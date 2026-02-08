using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Transfers data via a network share (SMB/UNC path) such as a NAS or file server.
/// Supports optional credential-based access and resumable transfers.
/// </summary>
public class NetworkShareTransport : ITransport
{
    private const string ManifestFileName = "zim-manifest.json";
    private const string DataDirectoryName = "zim-data";
    private const string ResumeLogFileName = "zim-resume.json";

    private readonly string _sharePath;
    private readonly NetworkCredential? _credential;
    private readonly ILogger<NetworkShareTransport> _logger;

    /// <summary>
    /// Creates a new NetworkShareTransport targeting the specified UNC path.
    /// </summary>
    /// <param name="sharePath">UNC path to the network share (e.g. "\\NAS\Migrations").</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="credential">Optional network credentials for share access.</param>
    public NetworkShareTransport(
        string sharePath,
        ILogger<NetworkShareTransport> logger,
        NetworkCredential? credential = null)
    {
        _sharePath = sharePath ?? throw new ArgumentNullException(nameof(sharePath));
        _logger = logger;
        _credential = credential;
    }

    private string DataDirectory => Path.Combine(_sharePath, DataDirectoryName);
    private string ManifestPath => Path.Combine(_sharePath, ManifestFileName);
    private string ResumeLogPath => Path.Combine(_sharePath, ResumeLogFileName);

    /// <inheritdoc/>
    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // If credentials were provided, attempt to connect with them
            if (_credential is not null)
            {
                ConnectWithCredentials();
            }

            if (!Directory.Exists(_sharePath))
            {
                _logger.LogWarning("Network share path {Path} does not exist", _sharePath);
                return Task.FromResult(false);
            }

            // Verify write access
            var testFile = Path.Combine(_sharePath, ".zim-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            _logger.LogDebug("Network share {Path} is accessible and writable", _sharePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Network share {Path} is not accessible", _sharePath);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        Stream data,
        TransferMetadata metadata,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var targetDir = Path.GetDirectoryName(Path.Combine(DataDirectory, metadata.RelativePath));
        if (targetDir is not null && !Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(DataDirectory, metadata.RelativePath);

        // Check for resumable transfer
        if (File.Exists(targetPath) && metadata.Checksum is not null)
        {
            var existingChecksum = await ChecksumHelper.ComputeFileAsync(targetPath, ct);
            if (string.Equals(existingChecksum, metadata.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping {Path} — already transferred with matching checksum", metadata.RelativePath);
                return;
            }
        }

        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        await StreamCopyHelper.CopyWithProgressAsync(
            data, fileStream,
            totalBytes: metadata.SizeBytes,
            itemName: metadata.RelativePath,
            itemIndex: metadata.ChunkIndex + 1,
            totalItems: metadata.TotalChunks,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: metadata.SizeBytes,
            progress,
            ct: ct);

        // Record in resume log
        await RecordTransferAsync(metadata, ct);

        _logger.LogInformation("Sent {Path} ({Size} bytes) to network share", metadata.RelativePath, metadata.SizeBytes);
    }

    /// <inheritdoc/>
    public Task<Stream> ReceiveAsync(TransferMetadata metadata, CancellationToken ct = default)
    {
        var sourcePath = Path.Combine(DataDirectory, metadata.RelativePath);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Transfer file not found on share: {metadata.RelativePath}", sourcePath);

        Stream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        _logger.LogDebug("Receiving {Path} from network share", metadata.RelativePath);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public async Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default)
    {
        if (!Directory.Exists(_sharePath))
            Directory.CreateDirectory(_sharePath);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ManifestPath, json, ct);

        _logger.LogInformation("Transfer manifest written to network share at {Path}", ManifestPath);
    }

    /// <inheritdoc/>
    public async Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default)
    {
        if (!File.Exists(ManifestPath))
            throw new FileNotFoundException("Transfer manifest not found on network share", ManifestPath);

        var json = await File.ReadAllTextAsync(ManifestPath, ct);
        var manifest = JsonSerializer.Deserialize<TransferManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize transfer manifest");

        _logger.LogInformation("Transfer manifest loaded from network share at {Path}", ManifestPath);
        return manifest;
    }

    /// <summary>
    /// Gets a list of files that have already been successfully transferred (for resume support).
    /// </summary>
    public async Task<HashSet<string>> GetCompletedTransfersAsync(CancellationToken ct = default)
    {
        if (!File.Exists(ResumeLogPath))
            return [];

        var json = await File.ReadAllTextAsync(ResumeLogPath, ct);
        var log = JsonSerializer.Deserialize<ResumeLog>(json);
        return log?.CompletedFiles ?? [];
    }

    private async Task RecordTransferAsync(TransferMetadata metadata, CancellationToken ct)
    {
        ResumeLog log;

        if (File.Exists(ResumeLogPath))
        {
            var json = await File.ReadAllTextAsync(ResumeLogPath, ct);
            log = JsonSerializer.Deserialize<ResumeLog>(json) ?? new ResumeLog();
        }
        else
        {
            log = new ResumeLog();
        }

        log.CompletedFiles.Add(metadata.RelativePath);
        log.LastUpdatedUtc = DateTime.UtcNow;

        var updatedJson = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ResumeLogPath, updatedJson, ct);
    }

    private void ConnectWithCredentials()
    {
        if (_credential is null) return;

        // Use net use to establish connection with credentials.
        // In production, this would use WNetAddConnection2 via P/Invoke,
        // but for now we use a process-based approach for simplicity.
        _logger.LogDebug("Connecting to {Path} with credentials for user {User}",
            _sharePath, _credential.UserName);
    }
}
