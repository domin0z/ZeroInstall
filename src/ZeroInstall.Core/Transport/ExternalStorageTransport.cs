using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Transfers data via USB drives or external HDDs using a local file path as the staging area.
/// Supports resumable transfers via per-file checksum tracking.
/// </summary>
public class ExternalStorageTransport : ITransport
{
    private const string ManifestFileName = "zim-manifest.json";
    private const string DataDirectoryName = "zim-data";
    private const string ResumeLogFileName = "zim-resume.json";

    private readonly string _basePath;
    private readonly ILogger<ExternalStorageTransport> _logger;

    /// <summary>
    /// Creates a new ExternalStorageTransport targeting the specified path.
    /// </summary>
    /// <param name="basePath">Root path on the external drive (e.g. "E:\ZeroInstall").</param>
    /// <param name="logger">Logger instance.</param>
    public ExternalStorageTransport(string basePath, ILogger<ExternalStorageTransport> logger)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _logger = logger;
    }

    private string DataDirectory => Path.Combine(_basePath, DataDirectoryName);
    private string ManifestPath => Path.Combine(_basePath, ManifestFileName);
    private string ResumeLogPath => Path.Combine(_basePath, ResumeLogFileName);

    /// <inheritdoc/>
    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);

            // Write and delete a test file to verify write access
            var testFile = Path.Combine(_basePath, ".zim-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            _logger.LogDebug("External storage path {Path} is accessible and writable", _basePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External storage path {Path} is not accessible", _basePath);
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

        // Check for resumable transfer — if file exists and checksum matches, skip
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

        _logger.LogInformation("Sent {Path} ({Size} bytes)", metadata.RelativePath, metadata.SizeBytes);
    }

    /// <inheritdoc/>
    public Task<Stream> ReceiveAsync(TransferMetadata metadata, CancellationToken ct = default)
    {
        var sourcePath = Path.Combine(DataDirectory, metadata.RelativePath);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Transfer file not found: {metadata.RelativePath}", sourcePath);

        Stream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        _logger.LogDebug("Receiving {Path} from external storage", metadata.RelativePath);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public async Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ManifestPath, json, ct);

        _logger.LogInformation("Transfer manifest written to {Path}", ManifestPath);
    }

    /// <inheritdoc/>
    public async Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default)
    {
        if (!File.Exists(ManifestPath))
            throw new FileNotFoundException("Transfer manifest not found", ManifestPath);

        var json = await File.ReadAllTextAsync(ManifestPath, ct);
        var manifest = JsonSerializer.Deserialize<TransferManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize transfer manifest");

        _logger.LogInformation("Transfer manifest loaded from {Path}", ManifestPath);
        return manifest;
    }

    /// <summary>
    /// Gets available external/removable drives with their free space.
    /// </summary>
    public static List<DriveInformation> GetAvailableDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Removable or DriveType.Fixed or DriveType.Network)
            .Where(d => !IsSystemDrive(d))
            .Select(d => new DriveInformation
            {
                DriveLetter = d.Name,
                Label = d.VolumeLabel,
                DriveType = d.DriveType,
                TotalSizeBytes = d.TotalSize,
                FreeSpaceBytes = d.AvailableFreeSpace
            })
            .ToList();
    }

    /// <summary>
    /// Validates that the target drive has enough free space for the transfer.
    /// </summary>
    public bool HasSufficientSpace(long requiredBytes)
    {
        try
        {
            var root = Path.GetPathRoot(_basePath);
            if (root is null) return false;

            var drive = new DriveInfo(root);
            return drive.IsReady && drive.AvailableFreeSpace >= requiredBytes;
        }
        catch
        {
            return false;
        }
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

    /// <summary>
    /// Cleans up all transfer data from the external storage.
    /// </summary>
    public void Cleanup()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
            _logger.LogInformation("Cleaned up transfer data at {Path}", _basePath);
        }
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

    private static bool IsSystemDrive(DriveInfo drive)
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(systemRoot)) return false;

        var systemDrive = Path.GetPathRoot(systemRoot);
        return string.Equals(drive.Name, systemDrive, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Information about an available drive for external storage transport.
/// </summary>
public class DriveInformation
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DriveType DriveType { get; set; }
    public long TotalSizeBytes { get; set; }
    public long FreeSpaceBytes { get; set; }
}

/// <summary>
/// Tracks which files have been successfully transferred for resume support.
/// </summary>
internal class ResumeLog
{
    public HashSet<string> CompletedFiles { get; set; } = [];
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
