using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Transfers data via SFTP to/from a remote NAS or server.
/// Supports chunked uploads (256 MB), resumable transfers, optional compression and AES-256 encryption.
/// </summary>
public class SftpTransport : ITransport, IDisposable
{
    internal const string ManifestFileName = "zim-manifest.json";
    internal const string DataDirectoryName = "zim-data";
    internal const string ResumeLogFileName = "zim-resume.json";
    internal const long DefaultChunkSize = 256L * 1024 * 1024; // 256 MB

    private readonly ISftpClientWrapper _client;
    private readonly string _remoteBasePath;
    private readonly ILogger<SftpTransport> _logger;
    private readonly string? _encryptionPassphrase;
    private readonly bool _compressBeforeUpload;

    public SftpTransport(
        ISftpClientWrapper sftpClient,
        string remoteBasePath,
        ILogger<SftpTransport> logger,
        string? encryptionPassphrase = null,
        bool compressBeforeUpload = true)
    {
        _client = sftpClient ?? throw new ArgumentNullException(nameof(sftpClient));
        _remoteBasePath = remoteBasePath ?? throw new ArgumentNullException(nameof(remoteBasePath));
        _logger = logger;
        _encryptionPassphrase = encryptionPassphrase;
        _compressBeforeUpload = compressBeforeUpload;
    }

    private string DataDirectory => _remoteBasePath + "/" + DataDirectoryName;
    private string ManifestPath => _remoteBasePath + "/" + ManifestFileName;
    private string ResumeLogPath => _remoteBasePath + "/" + ResumeLogFileName;

    /// <inheritdoc/>
    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_client.IsConnected)
                _client.Connect();

            EnsureRemoteDirectoryExists(_remoteBasePath);

            // Write and delete a test file to verify write access
            var testPath = _remoteBasePath + "/.zim-test";
            using (var ms = new MemoryStream(new byte[] { 0x42 }))
            {
                _client.UploadFile(ms, testPath);
            }
            _client.DeleteFile(testPath);

            _logger.LogDebug("SFTP path {Path} is accessible and writable", _remoteBasePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SFTP path {Path} is not accessible", _remoteBasePath);
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
        if (!_client.IsConnected)
            _client.Connect();

        var remoteDir = DataDirectory + "/" + GetParentPath(metadata.RelativePath);
        if (!string.IsNullOrEmpty(remoteDir))
            EnsureRemoteDirectoryExists(remoteDir);

        // Load completed chunks for resume support
        var completedChunks = await GetCompletedTransfersAsync(ct);

        // Process the data: optional compress -> optional encrypt
        Stream processedStream = data;
        MemoryStream? tempStream = null;

        try
        {
            if (_compressBeforeUpload)
            {
                var compressedStream = new MemoryStream();
                await CompressionHelper.CompressAsync(processedStream, compressedStream, ct);
                compressedStream.Position = 0;
                tempStream = compressedStream;
                processedStream = compressedStream;
                metadata.IsCompressed = true;
            }

            if (!string.IsNullOrEmpty(_encryptionPassphrase))
            {
                var encryptedStream = new MemoryStream();
                await EncryptionHelper.EncryptAsync(processedStream, encryptedStream, _encryptionPassphrase, ct);
                encryptedStream.Position = 0;
                tempStream?.Dispose();
                tempStream = encryptedStream;
                processedStream = encryptedStream;
                metadata.IsEncrypted = true;
            }

            var totalSize = processedStream.Length;

            if (totalSize > DefaultChunkSize)
            {
                // Chunked upload
                var totalChunks = (int)Math.Ceiling((double)totalSize / DefaultChunkSize);
                metadata.TotalChunks = totalChunks;

                for (var i = 0; i < totalChunks; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var chunkName = $"{metadata.RelativePath}.part{i:D4}";

                    // Resume: skip completed chunks
                    if (completedChunks.Contains(chunkName))
                    {
                        _logger.LogDebug("Skipping chunk {Chunk} -- already uploaded", chunkName);
                        processedStream.Position = Math.Min((long)(i + 1) * DefaultChunkSize, totalSize);
                        continue;
                    }

                    var chunkSize = (int)Math.Min(DefaultChunkSize, totalSize - processedStream.Position);
                    var chunkData = new byte[chunkSize];
                    await processedStream.ReadExactlyAsync(chunkData, ct);

                    var checksum = ChecksumHelper.Compute(chunkData);

                    var remoteTmpPath = DataDirectory + "/" + chunkName + ".tmp";
                    var remoteFinalPath = DataDirectory + "/" + chunkName;

                    using (var chunkStream = new MemoryStream(chunkData))
                    {
                        _client.UploadFile(chunkStream, remoteTmpPath);
                    }
                    _client.RenameFile(remoteTmpPath, remoteFinalPath);

                    await RecordTransferAsync(chunkName, checksum, ct);

                    progress?.Report(new TransferProgress
                    {
                        CurrentItemName = metadata.RelativePath,
                        CurrentItemIndex = i + 1,
                        TotalItems = totalChunks,
                        CurrentItemBytesTransferred = chunkSize,
                        CurrentItemTotalBytes = chunkSize,
                        OverallBytesTransferred = processedStream.Position,
                        OverallTotalBytes = totalSize
                    });

                    _logger.LogDebug("Uploaded chunk {Index}/{Total} for {Path}", i + 1, totalChunks, metadata.RelativePath);
                }
            }
            else
            {
                // Single file upload
                var checksum = await ChecksumHelper.ComputeAsync(processedStream, ct);

                if (completedChunks.Contains(metadata.RelativePath))
                {
                    _logger.LogDebug("Skipping {Path} -- already uploaded", metadata.RelativePath);
                    return;
                }

                var remoteTmpPath = DataDirectory + "/" + metadata.RelativePath + ".tmp";
                var remoteFinalPath = DataDirectory + "/" + metadata.RelativePath;

                _client.UploadFile(processedStream, remoteTmpPath);
                _client.RenameFile(remoteTmpPath, remoteFinalPath);

                await RecordTransferAsync(metadata.RelativePath, checksum, ct);

                _logger.LogInformation("Sent {Path} ({Size} bytes) via SFTP", metadata.RelativePath, totalSize);
            }
        }
        finally
        {
            tempStream?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> ReceiveAsync(TransferMetadata metadata, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        var remotePath = DataDirectory + "/" + metadata.RelativePath;
        Stream rawStream;

        // Check if chunked
        var firstChunkPath = DataDirectory + "/" + metadata.RelativePath + ".part0000";
        if (_client.Exists(firstChunkPath))
        {
            // Reassemble chunks
            var reassembled = new MemoryStream();
            var chunkIndex = 0;
            while (true)
            {
                var chunkPath = DataDirectory + "/" + metadata.RelativePath + $".part{chunkIndex:D4}";
                if (!_client.Exists(chunkPath))
                    break;

                var chunkStream = new MemoryStream();
                _client.DownloadFile(chunkPath, chunkStream);
                chunkStream.Position = 0;
                await chunkStream.CopyToAsync(reassembled, ct);
                chunkStream.Dispose();
                chunkIndex++;
            }
            reassembled.Position = 0;
            rawStream = reassembled;

            _logger.LogDebug("Reassembled {Count} chunks for {Path}", chunkIndex, metadata.RelativePath);
        }
        else if (_client.Exists(remotePath))
        {
            var ms = new MemoryStream();
            _client.DownloadFile(remotePath, ms);
            ms.Position = 0;
            rawStream = ms;
        }
        else
        {
            throw new FileNotFoundException($"Remote transfer file not found: {metadata.RelativePath}", remotePath);
        }

        // Process: optional decrypt -> optional decompress
        if (metadata.IsEncrypted && !string.IsNullOrEmpty(_encryptionPassphrase))
        {
            var decryptedStream = new MemoryStream();
            await EncryptionHelper.DecryptAsync(rawStream, decryptedStream, _encryptionPassphrase, ct);
            rawStream.Dispose();
            decryptedStream.Position = 0;
            rawStream = decryptedStream;
        }

        if (metadata.IsCompressed)
        {
            var decompressedStream = new MemoryStream();
            await CompressionHelper.DecompressAsync(rawStream, decompressedStream, ct);
            rawStream.Dispose();
            decompressedStream.Position = 0;
            rawStream = decompressedStream;
        }

        _logger.LogDebug("Receiving {Path} from SFTP", metadata.RelativePath);
        return rawStream;
    }

    /// <inheritdoc/>
    public async Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        EnsureRemoteDirectoryExists(_remoteBasePath);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var data = System.Text.Encoding.UTF8.GetBytes(json);

        if (!string.IsNullOrEmpty(_encryptionPassphrase))
        {
            data = await EncryptionHelper.EncryptBytesAsync(data, _encryptionPassphrase, ct);
        }

        using var ms = new MemoryStream(data);
        _client.UploadFile(ms, ManifestPath);

        _logger.LogInformation("Transfer manifest written to {Path}", ManifestPath);
    }

    /// <inheritdoc/>
    public async Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        if (!_client.Exists(ManifestPath))
            throw new FileNotFoundException("Transfer manifest not found", ManifestPath);

        using var ms = new MemoryStream();
        _client.DownloadFile(ManifestPath, ms);
        var data = ms.ToArray();

        if (!string.IsNullOrEmpty(_encryptionPassphrase))
        {
            data = await EncryptionHelper.DecryptBytesAsync(data, _encryptionPassphrase, ct);
        }

        var json = System.Text.Encoding.UTF8.GetString(data);
        var manifest = JsonSerializer.Deserialize<TransferManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize transfer manifest");

        _logger.LogInformation("Transfer manifest loaded from {Path}", ManifestPath);
        return manifest;
    }

    /// <summary>
    /// Gets a list of files/chunks that have been successfully uploaded (for resume support).
    /// </summary>
    public Task<HashSet<string>> GetCompletedTransfersAsync(CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        if (!_client.Exists(ResumeLogPath))
            return Task.FromResult<HashSet<string>>([]);

        using var ms = new MemoryStream();
        _client.DownloadFile(ResumeLogPath, ms);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var log = JsonSerializer.Deserialize<SftpResumeLog>(json);
        return Task.FromResult(log?.CompletedFiles ?? []);
    }

    /// <summary>
    /// Lists files and directories at the given remote path (for NAS browser UI).
    /// </summary>
    public Task<List<SftpFileInfo>> ListRemoteDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        var items = _client.ListDirectory(path).ToList();
        return Task.FromResult(items);
    }

    /// <summary>
    /// Creates a directory at the given remote path (recursive).
    /// </summary>
    public Task CreateRemoteDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            _client.Connect();

        EnsureRemoteDirectoryExists(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures all parent directories exist for the given remote path.
    /// </summary>
    internal void EnsureRemoteDirectoryExists(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var segment in segments)
        {
            current += "/" + segment;
            if (!_client.Exists(current))
            {
                _client.CreateDirectory(current);
                _logger.LogDebug("Created remote directory: {Path}", current);
            }
        }
    }

    private Task RecordTransferAsync(string name, string checksum, CancellationToken ct)
    {
        SftpResumeLog log;

        if (_client.Exists(ResumeLogPath))
        {
            using var ms = new MemoryStream();
            _client.DownloadFile(ResumeLogPath, ms);
            var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            log = JsonSerializer.Deserialize<SftpResumeLog>(json) ?? new SftpResumeLog();
        }
        else
        {
            log = new SftpResumeLog();
        }

        log.CompletedFiles.Add(name);
        log.Checksums[name] = checksum;
        log.LastUpdatedUtc = DateTime.UtcNow;

        var updatedJson = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
        using var uploadMs = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(updatedJson));
        _client.UploadFile(uploadMs, ResumeLogPath);
        return Task.CompletedTask;
    }

    private static string GetParentPath(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash >= 0 ? relativePath[..lastSlash] : string.Empty;
    }

    public void Dispose()
    {
        if (_client.IsConnected)
            _client.Disconnect();
        _client.Dispose();
    }
}

/// <summary>
/// Tracks which files/chunks have been successfully uploaded for SFTP resume support.
/// </summary>
internal class SftpResumeLog
{
    public HashSet<string> CompletedFiles { get; set; } = [];
    public Dictionary<string, string> Checksums { get; set; } = [];
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
