using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and restores application files (Program Files, AppData, ProgramData)
/// for Tier 2 registry+file migration.
/// </summary>
public class FileCaptureService
{
    private const string FileManifestFileName = "file-manifest.json";

    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger<FileCaptureService> _logger;

    public FileCaptureService(
        IFileSystemAccessor fileSystem,
        ILogger<FileCaptureService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Captures all file directories associated with the given applications.
    /// </summary>
    public async Task<FileCaptureManifest> CaptureAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        string outputDir,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var manifest = new FileCaptureManifest();
        long totalBytesCaptured = 0;

        for (int i = 0; i < apps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var app = apps[i];
            var appDirName = SanitizeDirName(app.Name);
            var appOutputDir = Path.Combine(outputDir, appDirName);

            progress?.Report(new TransferProgress
            {
                CurrentItemName = $"Capturing files: {app.Name}",
                CurrentItemIndex = i + 1,
                TotalItems = apps.Count,
                OverallBytesTransferred = totalBytesCaptured,
                OverallTotalBytes = apps.Sum(a => a.EstimatedSizeBytes)
            });

            var appEntry = new FileCaptureAppEntry
            {
                ApplicationName = app.Name,
                DirectoryName = appDirName
            };

            // 1. Capture install location (Program Files)
            if (!string.IsNullOrEmpty(app.InstallLocation) && _fileSystem.DirectoryExists(app.InstallLocation))
            {
                var installDir = Path.Combine(appOutputDir, "install");
                var bytesCopied = CopyDirectoryWithTimestamps(app.InstallLocation, installDir);
                appEntry.CapturedPaths.Add(new FileCapturePathEntry
                {
                    OriginalPath = app.InstallLocation,
                    CaptureSubDir = "install",
                    PathCategory = FilePathCategory.InstallLocation,
                    SizeBytes = bytesCopied
                });
                totalBytesCaptured += bytesCopied;
                _logger.LogDebug("Captured install location: {Path} ({Bytes} bytes)", app.InstallLocation, bytesCopied);
            }

            // 2. Capture AppData paths
            for (int j = 0; j < app.AppDataPaths.Count; j++)
            {
                var appDataPath = app.AppDataPaths[j];
                if (!_fileSystem.DirectoryExists(appDataPath)) continue;

                var subDir = $"appdata-{j}";
                var destDir = Path.Combine(appOutputDir, subDir);
                var bytesCopied = CopyDirectoryWithTimestamps(appDataPath, destDir);

                appEntry.CapturedPaths.Add(new FileCapturePathEntry
                {
                    OriginalPath = appDataPath,
                    CaptureSubDir = subDir,
                    PathCategory = CategorizeAppDataPath(appDataPath),
                    SizeBytes = bytesCopied
                });
                totalBytesCaptured += bytesCopied;
                _logger.LogDebug("Captured AppData: {Path} ({Bytes} bytes)", appDataPath, bytesCopied);
            }

            // 3. Capture ProgramData paths
            var programDataPaths = FindProgramDataPaths(app);
            for (int j = 0; j < programDataPaths.Count; j++)
            {
                var pdPath = programDataPaths[j];
                if (!_fileSystem.DirectoryExists(pdPath)) continue;

                var subDir = $"programdata-{j}";
                var destDir = Path.Combine(appOutputDir, subDir);
                var bytesCopied = CopyDirectoryWithTimestamps(pdPath, destDir);

                appEntry.CapturedPaths.Add(new FileCapturePathEntry
                {
                    OriginalPath = pdPath,
                    CaptureSubDir = subDir,
                    PathCategory = FilePathCategory.ProgramData,
                    SizeBytes = bytesCopied
                });
                totalBytesCaptured += bytesCopied;
                _logger.LogDebug("Captured ProgramData: {Path} ({Bytes} bytes)", pdPath, bytesCopied);
            }

            if (appEntry.CapturedPaths.Count > 0)
                manifest.Apps.Add(appEntry);
        }

        // Write manifest
        var manifestPath = Path.Combine(outputDir, FileManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("File capture complete: {AppCount} apps, {TotalBytes} bytes",
            manifest.Apps.Count, totalBytesCaptured);

        return manifest;
    }

    /// <summary>
    /// Restores previously captured files to matching locations on the destination.
    /// </summary>
    public async Task RestoreAsync(
        string inputDir,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(inputDir, FileManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("No file manifest found at {Path}, skipping restore", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<FileCaptureManifest>(json);
        if (manifest is null) return;

        long totalBytesRestored = 0;
        var totalBytes = manifest.Apps.SelectMany(a => a.CapturedPaths).Sum(p => p.SizeBytes);

        for (int i = 0; i < manifest.Apps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var appEntry = manifest.Apps[i];

            progress?.Report(new TransferProgress
            {
                CurrentItemName = $"Restoring: {appEntry.ApplicationName}",
                CurrentItemIndex = i + 1,
                TotalItems = manifest.Apps.Count,
                OverallBytesTransferred = totalBytesRestored,
                OverallTotalBytes = totalBytes
            });

            foreach (var pathEntry in appEntry.CapturedPaths)
            {
                var sourceDir = Path.Combine(inputDir, appEntry.DirectoryName, pathEntry.CaptureSubDir);
                if (!Directory.Exists(sourceDir))
                {
                    _logger.LogWarning("Captured directory not found: {Dir}", sourceDir);
                    continue;
                }

                // Apply user path remapping for AppData paths
                var targetPath = pathEntry.OriginalPath;
                if (pathEntry.PathCategory is FilePathCategory.AppDataRoaming
                    or FilePathCategory.AppDataLocal
                    or FilePathCategory.AppDataLocalLow)
                {
                    targetPath = AppDataCaptureHelper.RemapPathForUser(targetPath, userMappings);
                }

                try
                {
                    var bytesCopied = CopyDirectoryWithTimestamps(sourceDir, targetPath);
                    totalBytesRestored += bytesCopied;
                    _logger.LogDebug("Restored {Path} ({Bytes} bytes)", targetPath, bytesCopied);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore files to {Path}", targetPath);
                }
            }
        }

        _logger.LogInformation("File restore complete: {AppCount} apps, {TotalBytes} bytes",
            manifest.Apps.Count, totalBytesRestored);
    }

    /// <summary>
    /// Finds ProgramData paths for an application based on name/publisher.
    /// </summary>
    internal List<string> FindProgramDataPaths(DiscoveredApplication app)
    {
        var paths = new List<string>();
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrEmpty(programData)) return paths;

        var searchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(app.Name)) searchNames.Add(app.Name);
        if (!string.IsNullOrEmpty(app.Publisher)) searchNames.Add(app.Publisher);

        foreach (var name in searchNames)
        {
            var path = Path.Combine(programData, name);
            if (_fileSystem.DirectoryExists(path))
                paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// Copies a directory recursively, preserving file timestamps.
    /// Handles locked/inaccessible files by logging and skipping.
    /// </summary>
    internal long CopyDirectoryWithTimestamps(string sourceDir, string targetDir)
    {
        long totalBytes = 0;
        Directory.CreateDirectory(targetDir);

        // Copy files
        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            try
            {
                var destFile = Path.Combine(targetDir, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destFile, overwrite: true);

                // Preserve timestamps
                var info = new FileInfo(sourceFile);
                File.SetCreationTimeUtc(destFile, info.CreationTimeUtc);
                File.SetLastWriteTimeUtc(destFile, info.LastWriteTimeUtc);
                File.SetLastAccessTimeUtc(destFile, info.LastAccessTimeUtc);

                totalBytes += info.Length;
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Skipping locked file: {File} ({Message})", sourceFile, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Skipping inaccessible file: {File} ({Message})", sourceFile, ex.Message);
            }
        }

        // Copy subdirectories
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            try
            {
                var destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                totalBytes += CopyDirectoryWithTimestamps(subDir, destSubDir);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Skipping inaccessible directory: {Dir} ({Message})", subDir, ex.Message);
            }
        }

        return totalBytes;
    }

    internal static FilePathCategory CategorizeAppDataPath(string path)
    {
        if (path.Contains("AppData\\Roaming", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("AppData/Roaming", StringComparison.OrdinalIgnoreCase))
            return FilePathCategory.AppDataRoaming;

        if (path.Contains("AppData\\LocalLow", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("AppData/LocalLow", StringComparison.OrdinalIgnoreCase))
            return FilePathCategory.AppDataLocalLow;

        if (path.Contains("AppData\\Local", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("AppData/Local", StringComparison.OrdinalIgnoreCase))
            return FilePathCategory.AppDataLocal;

        return FilePathCategory.AppDataRoaming; // Default
    }

    internal static string SanitizeDirName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().TrimEnd('.');
    }
}

/// <summary>
/// Manifest describing all captured files for Tier 2 migration.
/// </summary>
public class FileCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<FileCaptureAppEntry> Apps { get; set; } = [];
}

/// <summary>
/// File capture data for a single application.
/// </summary>
public class FileCaptureAppEntry
{
    public string ApplicationName { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public List<FileCapturePathEntry> CapturedPaths { get; set; } = [];
}

/// <summary>
/// A single captured directory path.
/// </summary>
public class FileCapturePathEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string CaptureSubDir { get; set; } = string.Empty;
    public FilePathCategory PathCategory { get; set; }
    public long SizeBytes { get; set; }
}

/// <summary>
/// Category of a captured file path, used for restore logic.
/// </summary>
public enum FilePathCategory
{
    InstallLocation,
    AppDataRoaming,
    AppDataLocal,
    AppDataLocalLow,
    ProgramData
}
