using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and restores application data (AppData directories and HKCU registry keys)
/// for settings overlay after package-based install.
/// </summary>
public class AppDataCaptureHelper
{
    private const string AppDataManifestFileName = "appdata-manifest.json";
    private const string RegistryExportDirName = "registry";
    private const string FileDataDirName = "files";

    private readonly IFileSystemAccessor _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<AppDataCaptureHelper> _logger;

    public AppDataCaptureHelper(
        IFileSystemAccessor fileSystem,
        IProcessRunner processRunner,
        ILogger<AppDataCaptureHelper> logger)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Captures AppData folders and HKCU registry keys for the given app.
    /// </summary>
    /// <param name="app">The discovered application to capture settings for.</param>
    /// <param name="outputDir">Root output directory for this app's captured data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A manifest describing what was captured.</returns>
    public async Task<AppDataCaptureManifest> CaptureAsync(
        DiscoveredApplication app,
        string outputDir,
        CancellationToken ct = default)
    {
        var manifest = new AppDataCaptureManifest
        {
            ApplicationName = app.Name,
            ApplicationVersion = app.Version
        };

        Directory.CreateDirectory(outputDir);

        // Capture AppData directories
        await CaptureAppDataPathsAsync(app, outputDir, manifest, ct);

        // Capture HKCU registry keys for this app
        await CaptureRegistryKeysAsync(app, outputDir, manifest, ct);

        // Write manifest
        var manifestPath = Path.Combine(outputDir, AppDataManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Captured settings for {App}: {FileCount} paths, {RegCount} registry exports",
            app.Name, manifest.CapturedPaths.Count, manifest.CapturedRegistryFiles.Count);

        return manifest;
    }

    /// <summary>
    /// Restores previously captured AppData and registry keys to the destination.
    /// </summary>
    /// <param name="captureDir">Directory containing captured data.</param>
    /// <param name="userMappings">User mappings for path remapping.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RestoreAsync(
        string captureDir,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(captureDir, AppDataManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("No AppData manifest found at {Path}, skipping restore", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<AppDataCaptureManifest>(json);
        if (manifest is null) return;

        // Restore file data
        await RestoreFileDataAsync(captureDir, manifest, userMappings, ct);

        // Restore registry keys
        await RestoreRegistryKeysAsync(captureDir, manifest, ct);

        _logger.LogInformation("Restored settings for {App}", manifest.ApplicationName);
    }

    private async Task CaptureAppDataPathsAsync(
        DiscoveredApplication app,
        string outputDir,
        AppDataCaptureManifest manifest,
        CancellationToken ct)
    {
        var fileDataDir = Path.Combine(outputDir, FileDataDirName);

        foreach (var sourcePath in app.AppDataPaths)
        {
            if (!_fileSystem.DirectoryExists(sourcePath))
                continue;

            // Create a relative identifier for this path
            var pathId = GeneratePathId(sourcePath);
            var destDir = Path.Combine(fileDataDir, pathId);

            try
            {
                CopyDirectoryRecursive(sourcePath, destDir);
                manifest.CapturedPaths.Add(new CapturedPathEntry
                {
                    OriginalPath = sourcePath,
                    PathId = pathId,
                    SizeBytes = _fileSystem.GetDirectorySize(sourcePath)
                });

                _logger.LogDebug("Captured AppData path: {Path}", sourcePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture AppData path {Path}", sourcePath);
            }
        }

        await Task.CompletedTask; // Async signature for consistency
    }

    private async Task CaptureRegistryKeysAsync(
        DiscoveredApplication app,
        string outputDir,
        AppDataCaptureManifest manifest,
        CancellationToken ct)
    {
        var regDir = Path.Combine(outputDir, RegistryExportDirName);
        Directory.CreateDirectory(regDir);

        // Export the app's HKCU uninstall key if it's a per-user install
        var keysToExport = new List<string>();

        if (app.IsPerUser && !string.IsNullOrEmpty(app.RegistryKeyPath))
        {
            keysToExport.Add($"HKCU\\{app.RegistryKeyPath}");
        }

        // Export additional registry paths specified for this app
        foreach (var path in app.AdditionalRegistryPaths)
        {
            keysToExport.Add(path);
        }

        // Try to find app-specific HKCU\Software keys by publisher/name
        var appNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { app.Name };
        if (!string.IsNullOrEmpty(app.Publisher))
            appNames.Add(app.Publisher);

        foreach (var name in appNames)
        {
            keysToExport.Add($@"HKCU\Software\{name}");
        }

        for (int i = 0; i < keysToExport.Count; i++)
        {
            var key = keysToExport[i];
            var exportFile = Path.Combine(regDir, $"reg-export-{i}.reg");

            try
            {
                var result = await _processRunner.RunAsync(
                    "reg", $"export \"{key}\" \"{exportFile}\" /y", ct);

                if (result.Success)
                {
                    manifest.CapturedRegistryFiles.Add(new CapturedRegistryEntry
                    {
                        RegistryKeyPath = key,
                        ExportFileName = $"reg-export-{i}.reg"
                    });
                    _logger.LogDebug("Exported registry key: {Key}", key);
                }
                else
                {
                    _logger.LogDebug("Registry key not found or empty: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to export registry key {Key}", key);
            }
        }
    }

    private async Task RestoreFileDataAsync(
        string captureDir,
        AppDataCaptureManifest manifest,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct)
    {
        var fileDataDir = Path.Combine(captureDir, FileDataDirName);

        foreach (var entry in manifest.CapturedPaths)
        {
            var sourceDir = Path.Combine(fileDataDir, entry.PathId);
            if (!Directory.Exists(sourceDir))
            {
                _logger.LogWarning("Captured path data not found: {PathId}", entry.PathId);
                continue;
            }

            // Determine the target path, applying user mapping if needed
            var targetPath = RemapPathForUser(entry.OriginalPath, userMappings);

            try
            {
                CopyDirectoryRecursive(sourceDir, targetPath);
                _logger.LogDebug("Restored AppData to {Path}", targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore AppData to {Path}", targetPath);
            }
        }

        await Task.CompletedTask;
    }

    private async Task RestoreRegistryKeysAsync(
        string captureDir,
        AppDataCaptureManifest manifest,
        CancellationToken ct)
    {
        var regDir = Path.Combine(captureDir, RegistryExportDirName);

        foreach (var entry in manifest.CapturedRegistryFiles)
        {
            var regFile = Path.Combine(regDir, entry.ExportFileName);
            if (!File.Exists(regFile))
            {
                _logger.LogWarning("Registry export file not found: {File}", entry.ExportFileName);
                continue;
            }

            try
            {
                var result = await _processRunner.RunAsync(
                    "reg", $"import \"{regFile}\"", ct);

                if (result.Success)
                    _logger.LogDebug("Imported registry key: {Key}", entry.RegistryKeyPath);
                else
                    _logger.LogWarning("Failed to import registry key {Key}: {Error}",
                        entry.RegistryKeyPath, result.StandardError);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import registry file {File}", entry.ExportFileName);
            }
        }
    }

    internal static string RemapPathForUser(string originalPath, IReadOnlyList<UserMapping> userMappings)
    {
        foreach (var mapping in userMappings)
        {
            if (!mapping.RequiresPathRemapping) continue;

            var sourcePrefix = mapping.SourcePathPrefix;
            var destPrefix = mapping.DestinationProfilePath;

            if (!string.IsNullOrEmpty(sourcePrefix) && !string.IsNullOrEmpty(destPrefix) &&
                originalPath.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return destPrefix + originalPath[sourcePrefix.Length..];
            }
        }

        return originalPath;
    }

    internal static string GeneratePathId(string path)
    {
        // Create a safe directory name from the path
        // e.g. "C:\Users\Bill\AppData\Roaming\Chrome" -> "Users_Bill_AppData_Roaming_Chrome"
        return path
            .Replace(":\\", "_")
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(" ", "-")
            .Trim('_');
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }
}

/// <summary>
/// Manifest describing captured app data for settings overlay.
/// </summary>
public class AppDataCaptureManifest
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<CapturedPathEntry> CapturedPaths { get; set; } = [];
    public List<CapturedRegistryEntry> CapturedRegistryFiles { get; set; } = [];
}

/// <summary>
/// A captured filesystem path (AppData directory).
/// </summary>
public class CapturedPathEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string PathId { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

/// <summary>
/// A captured registry export file.
/// </summary>
public class CapturedRegistryEntry
{
    public string RegistryKeyPath { get; set; } = string.Empty;
    public string ExportFileName { get; set; } = string.Empty;
}
