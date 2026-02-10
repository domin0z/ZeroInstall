using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Orchestrates file backup runs: scan, diff, check quota, enforce retention, upload changed files, update index.
/// </summary>
internal class BackupExecutor : IBackupExecutor
{
    private readonly IFileIndexService _indexService;
    private readonly IRetentionService _retentionService;
    private readonly ISftpClientFactory _sftpClientFactory;
    private readonly ILogger<BackupExecutor> _logger;

    /// <summary>
    /// Base directory for local data files (index, etc.). Defaults to AppContext.BaseDirectory.
    /// </summary>
    public string LocalDataPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data");

    public BackupExecutor(
        IFileIndexService indexService,
        IRetentionService retentionService,
        ISftpClientFactory sftpClientFactory,
        ILogger<BackupExecutor> logger)
    {
        _indexService = indexService;
        _retentionService = retentionService;
        _sftpClientFactory = sftpClientFactory;
        _logger = logger;
    }

    public async Task<BackupRunResult> RunFileBackupAsync(
        BackupConfiguration config,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var runId = DateTime.UtcNow.ToString("yyyy-MM-ddTHHmmssZ");
        var result = new BackupRunResult
        {
            RunId = runId,
            BackupType = "file",
            StartedUtc = DateTime.UtcNow
        };

        try
        {
            // Step 1: Load existing index
            statusProgress?.Report("Loading backup index...");
            var indexPath = GetLocalIndexPath(config.CustomerId);
            var index = await _indexService.LoadIndexAsync(indexPath, ct);

            // Step 2: Scan directories
            statusProgress?.Report("Scanning directories...");
            var currentFiles = await _indexService.ScanDirectoriesAsync(
                config.BackupPaths, config.ExcludePatterns, ct);
            result.FilesScanned = currentFiles.Count;

            // Step 3: Hash files for diff comparison
            statusProgress?.Report("Computing file hashes...");
            foreach (var file in currentFiles)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Find the full path by finding which backup path contains this relative path
                    var fullPath = ResolveFullPath(config.BackupPaths, file.RelativePath);
                    if (fullPath != null)
                        file.Sha256 = await FileIndexService.HashFileAsync(fullPath, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to hash {Path}", file.RelativePath);
                }
            }

            // Step 4: Compute diff
            var changedFiles = index.GetChangedFiles(currentFiles);
            _logger.LogInformation("Scan complete: {Total} files, {Changed} changed",
                currentFiles.Count, changedFiles.Count);

            if (changedFiles.Count == 0)
            {
                statusProgress?.Report("No changes detected.");
                result.ResultType = BackupRunResultType.Skipped;
                result.CompletedUtc = DateTime.UtcNow;

                // Update the index scan time even if nothing changed
                index.LastScanUtc = DateTime.UtcNow;
                index.CustomerId = config.CustomerId;
                index.Files = currentFiles;
                await _indexService.SaveIndexAsync(index, indexPath, ct);

                return result;
            }

            // Step 5: Connect to NAS
            statusProgress?.Report("Connecting to NAS...");
            using var client = _sftpClientFactory.Create(config.NasConnection);
            client.Connect();

            // Step 6: Check quota
            if (config.QuotaBytes > 0)
            {
                var usage = await _retentionService.CalculateNasUsageAsync(
                    client, config.GetNasCustomerPath(), ct);

                if (usage >= config.QuotaBytes)
                {
                    statusProgress?.Report("Quota exceeded, enforcing retention...");
                    await _retentionService.EnforceRetentionAsync(
                        client, config.GetNasFileBackupPath(),
                        config.Retention.KeepLastFileBackups, ct);

                    // Re-check
                    usage = await _retentionService.CalculateNasUsageAsync(
                        client, config.GetNasCustomerPath(), ct);

                    if (usage >= config.QuotaBytes)
                    {
                        result.ResultType = BackupRunResultType.QuotaExceeded;
                        result.CompletedUtc = DateTime.UtcNow;
                        result.Errors.Add($"Quota exceeded: {usage} bytes used of {config.QuotaBytes} bytes allowed");
                        return result;
                    }
                }
            }

            // Step 7: Enforce retention (even without quota)
            if (config.Retention.KeepLastFileBackups > 0)
            {
                await _retentionService.EnforceRetentionAsync(
                    client, config.GetNasFileBackupPath(),
                    config.Retention.KeepLastFileBackups, ct);
            }

            // Step 8: Create backup run directory on NAS
            var runPath = $"{config.GetNasFileBackupPath()}/{runId}";
            var dataPath = $"{runPath}/zim-data";
            EnsureRemoteDirectory(client, runPath);
            EnsureRemoteDirectory(client, dataPath);

            // Step 9: Upload changed files
            statusProgress?.Report($"Uploading {changedFiles.Count} files...");
            int uploaded = 0;
            int failed = 0;

            foreach (var file in changedFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var fullPath = ResolveFullPath(config.BackupPaths, file.RelativePath);
                    if (fullPath == null)
                    {
                        failed++;
                        result.Errors.Add($"Could not resolve path: {file.RelativePath}");
                        continue;
                    }

                    var remotePath = $"{dataPath}/{file.RelativePath}";
                    var remoteDir = remotePath.Substring(0, remotePath.LastIndexOf('/'));
                    EnsureRemoteDirectory(client, remoteDir);

                    await using var fileStream = File.OpenRead(fullPath);

                    if (config.CompressBeforeUpload)
                    {
                        using var compressed = new MemoryStream();
                        await CompressionHelper.CompressAsync(fileStream, compressed, ct);
                        compressed.Position = 0;

                        if (!string.IsNullOrEmpty(config.EncryptionPassphrase))
                        {
                            using var encrypted = new MemoryStream();
                            await EncryptionHelper.EncryptAsync(compressed, encrypted, config.EncryptionPassphrase, ct);
                            encrypted.Position = 0;
                            client.UploadFile(encrypted, remotePath);
                            result.BytesTransferred += encrypted.Length;
                        }
                        else
                        {
                            client.UploadFile(compressed, remotePath);
                            result.BytesTransferred += compressed.Length;
                        }
                    }
                    else if (!string.IsNullOrEmpty(config.EncryptionPassphrase))
                    {
                        using var encrypted = new MemoryStream();
                        await EncryptionHelper.EncryptAsync(fileStream, encrypted, config.EncryptionPassphrase, ct);
                        encrypted.Position = 0;
                        client.UploadFile(encrypted, remotePath);
                        result.BytesTransferred += encrypted.Length;
                    }
                    else
                    {
                        client.UploadFile(fileStream, remotePath);
                        result.BytesTransferred += file.SizeBytes;
                    }

                    file.BackedUpUtc = DateTime.UtcNow;
                    file.BackupRunId = runId;
                    uploaded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    result.Errors.Add($"{file.RelativePath}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to upload {Path}", file.RelativePath);
                }
            }

            // Step 10: Write manifest
            var manifest = new
            {
                RunId = runId,
                CreatedUtc = DateTime.UtcNow,
                FilesUploaded = uploaded,
                FilesFailed = failed,
                TotalScanned = currentFiles.Count
            };
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            using var manifestStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(manifestJson));
            client.UploadFile(manifestStream, $"{runPath}/zim-manifest.json");

            // Step 11: Update local index
            index.CustomerId = config.CustomerId;
            index.LastScanUtc = DateTime.UtcNow;
            index.Files = currentFiles;
            await _indexService.SaveIndexAsync(index, indexPath, ct);

            result.FilesUploaded = uploaded;
            result.FilesFailed = failed;
            result.ResultType = failed > 0
                ? BackupRunResultType.PartialSuccess
                : BackupRunResultType.Success;
            result.CompletedUtc = DateTime.UtcNow;

            statusProgress?.Report($"Backup complete: {uploaded} uploaded, {failed} failed");
        }
        catch (OperationCanceledException)
        {
            result.ResultType = BackupRunResultType.Failed;
            result.CompletedUtc = DateTime.UtcNow;
            result.Errors.Add("Backup cancelled");
            throw;
        }
        catch (Exception ex)
        {
            result.ResultType = BackupRunResultType.Failed;
            result.CompletedUtc = DateTime.UtcNow;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "File backup failed");
        }

        return result;
    }

    public async Task<BackupRunResult> RunFullImageBackupAsync(
        BackupConfiguration config,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var runId = DateTime.UtcNow.ToString("yyyy-MM-ddTHHmmssZ");
        var result = new BackupRunResult
        {
            RunId = runId,
            BackupType = "full-image",
            StartedUtc = DateTime.UtcNow
        };

        try
        {
            statusProgress?.Report("Full image backup is not yet implemented in the persistent agent.");
            result.ResultType = BackupRunResultType.Skipped;
            result.CompletedUtc = DateTime.UtcNow;
            result.Errors.Add("Full image backup via persistent agent is planned for a future release. Use the CLI or GUI for full disk clones.");
        }
        catch (Exception ex)
        {
            result.ResultType = BackupRunResultType.Failed;
            result.CompletedUtc = DateTime.UtcNow;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Full image backup failed");
        }

        return await Task.FromResult(result);
    }

    private string GetLocalIndexPath(string customerId)
    {
        return Path.Combine(LocalDataPath, $"backup-index-{customerId}.json");
    }

    private static string? ResolveFullPath(IReadOnlyList<string> backupPaths, string relativePath)
    {
        var osPath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        foreach (var basePath in backupPaths)
        {
            var candidate = Path.Combine(basePath, osPath);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static void EnsureRemoteDirectory(ISftpClientWrapper client, string path)
    {
        if (client.Exists(path))
            return;

        // Walk up to find an existing parent, then create directories down
        var parts = path.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();
        var current = "";

        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }
}
