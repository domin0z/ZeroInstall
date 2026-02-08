using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Tier 2 migrator: captures and replays registry keys, Program Files, AppData,
/// and ProgramData for applications that don't have package manager support.
/// </summary>
public class RegistryFileMigratorService : IRegistryMigrator
{
    private const string Tier2ManifestFileName = "tier2-manifest.json";
    private const string RegistrySubDir = "registry";
    private const string FilesSubDir = "files";

    private readonly RegistryCaptureService _registryCapture;
    private readonly FileCaptureService _fileCapture;
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger<RegistryFileMigratorService> _logger;

    public RegistryFileMigratorService(
        RegistryCaptureService registryCapture,
        FileCaptureService fileCapture,
        IProcessRunner processRunner,
        IFileSystemAccessor fileSystem,
        ILogger<RegistryFileMigratorService> logger)
    {
        _registryCapture = registryCapture;
        _fileCapture = fileCapture;
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Captures registry keys and files for all selected Tier 2 items.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputPath);

        var tier2Items = items
            .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.RegistryFile && i.SourceData is DiscoveredApplication)
            .ToList();

        var apps = tier2Items
            .Select(i => (DiscoveredApplication)i.SourceData!)
            .ToList();

        if (apps.Count == 0)
        {
            _logger.LogInformation("No Tier 2 items selected, skipping registry/file capture");
            return;
        }

        // Update item statuses
        foreach (var item in tier2Items)
            item.Status = MigrationItemStatus.InProgress;

        // Step 1: Export registry keys
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Exporting registry keys...",
            CurrentItemIndex = 1,
            TotalItems = 2
        });

        var regDir = Path.Combine(outputPath, RegistrySubDir);
        await _registryCapture.ExportAsync(apps, regDir, ct);

        // Step 2: Capture files
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Capturing application files...",
            CurrentItemIndex = 2,
            TotalItems = 2
        });

        var filesDir = Path.Combine(outputPath, FilesSubDir);
        var fileManifest = await _fileCapture.CaptureAsync(apps, filesDir, progress, ct);

        // Detect items that may need licensing warnings
        var licensingWarnings = DetectLicensingApps(apps);

        // Detect COM registrations
        var comApps = DetectComRegistrations(apps);

        // Write Tier 2 manifest
        var tier2Manifest = new Tier2CaptureManifest
        {
            ApplicationNames = apps.Select(a => a.Name).ToList(),
            LicensingWarnings = licensingWarnings,
            ComRegistrations = comApps
        };

        var manifestPath = Path.Combine(outputPath, Tier2ManifestFileName);
        var json = JsonSerializer.Serialize(tier2Manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        // Update item statuses
        foreach (var item in tier2Items)
            item.Status = MigrationItemStatus.Completed;

        _logger.LogInformation("Tier 2 capture complete: {Count} apps", apps.Count);
    }

    /// <summary>
    /// Restores registry keys and files on the destination machine.
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Read the Tier 2 manifest for warnings
        var tier2ManifestPath = Path.Combine(inputPath, Tier2ManifestFileName);
        Tier2CaptureManifest? tier2Manifest = null;
        if (File.Exists(tier2ManifestPath))
        {
            var json = await File.ReadAllTextAsync(tier2ManifestPath, ct);
            tier2Manifest = JsonSerializer.Deserialize<Tier2CaptureManifest>(json);
        }

        // Step 1: Restore files first (before registry, since registry may reference file paths)
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Restoring application files...",
            CurrentItemIndex = 1,
            TotalItems = 4
        });

        var filesDir = Path.Combine(inputPath, FilesSubDir);
        if (Directory.Exists(filesDir))
        {
            await _fileCapture.RestoreAsync(filesDir, userMappings, progress, ct);
        }

        // Step 2: Import registry keys
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Importing registry keys...",
            CurrentItemIndex = 2,
            TotalItems = 4
        });

        var regDir = Path.Combine(inputPath, RegistrySubDir);
        if (Directory.Exists(regDir))
        {
            await ImportRegistryAsync(regDir, userMappings, ct);
        }

        // Step 3: Create Start Menu shortcuts
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Creating Start Menu shortcuts...",
            CurrentItemIndex = 3,
            TotalItems = 4
        });

        await CreateStartMenuShortcutsAsync(inputPath, ct);

        // Step 4: Log warnings
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Finalizing...",
            CurrentItemIndex = 4,
            TotalItems = 4
        });

        if (tier2Manifest is not null)
        {
            foreach (var warning in tier2Manifest.LicensingWarnings)
            {
                _logger.LogWarning("Licensing: {App} may require manual activation or re-licensing", warning);
            }

            foreach (var com in tier2Manifest.ComRegistrations)
            {
                _logger.LogInformation("COM component detected: {App} — may need re-registration via regsvr32", com);
            }
        }

        _logger.LogInformation("Tier 2 restore complete");
    }

    /// <inheritdoc/>
    public async Task ExportRegistryAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        string outputPath,
        CancellationToken ct = default)
    {
        await _registryCapture.ExportAsync(apps, outputPath, ct);
    }

    /// <inheritdoc/>
    public async Task ImportRegistryAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct = default)
    {
        await _registryCapture.ImportAsync(inputPath, userMappings, ct);
    }

    /// <summary>
    /// Creates Start Menu shortcuts for restored applications by scanning for .exe files
    /// in the restored install locations.
    /// </summary>
    internal async Task CreateStartMenuShortcutsAsync(string inputPath, CancellationToken ct)
    {
        var filesDir = Path.Combine(inputPath, FilesSubDir);
        var fileManifestPath = Path.Combine(filesDir, "file-manifest.json");
        if (!File.Exists(fileManifestPath)) return;

        var json = await File.ReadAllTextAsync(fileManifestPath, ct);
        var fileManifest = JsonSerializer.Deserialize<FileCaptureManifest>(json);
        if (fileManifest is null) return;

        var startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs", "ZeroInstall Migrated");

        foreach (var appEntry in fileManifest.Apps)
        {
            var installEntry = appEntry.CapturedPaths
                .FirstOrDefault(p => p.PathCategory == FilePathCategory.InstallLocation);

            if (installEntry is null) continue;

            // Look for .exe files in the install location
            var installDir = installEntry.OriginalPath;
            if (!_fileSystem.DirectoryExists(installDir)) continue;

            var exeFiles = _fileSystem.GetFiles(installDir, "*.exe");
            if (exeFiles.Length == 0) continue;

            // Pick the most likely main executable (largest .exe or name matching app name)
            var mainExe = FindMainExecutable(exeFiles, appEntry.ApplicationName);
            if (mainExe is null) continue;

            try
            {
                Directory.CreateDirectory(startMenuPath);
                var shortcutPath = Path.Combine(startMenuPath, $"{appEntry.ApplicationName}.lnk");
                await CreateShortcutAsync(shortcutPath, mainExe, ct);
                _logger.LogDebug("Created Start Menu shortcut for {App}", appEntry.ApplicationName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create shortcut for {App}", appEntry.ApplicationName);
            }
        }
    }

    /// <summary>
    /// Detects applications that likely require manual licensing/activation.
    /// </summary>
    internal static List<string> DetectLicensingApps(IReadOnlyList<DiscoveredApplication> apps)
    {
        var licensingKeywords = new[]
        {
            "Microsoft Office", "Adobe", "AutoCAD", "Photoshop", "Illustrator",
            "Visual Studio", "JetBrains", "Sublime Text", "VMware", "Parallels",
            "Norton", "McAfee", "Kaspersky", "ESET", "Bitdefender",
            "CorelDRAW", "Corel", "MATLAB", "SolidWorks", "AutoDesk"
        };

        return apps
            .Where(a => licensingKeywords.Any(kw =>
                a.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .Select(a => a.Name)
            .ToList();
    }

    /// <summary>
    /// Detects applications that may have COM registrations.
    /// </summary>
    internal static List<string> DetectComRegistrations(IReadOnlyList<DiscoveredApplication> apps)
    {
        // Apps with COM registrations typically have these in their registry
        // or are from publishers known to use COM heavily
        var comPublishers = new[]
        {
            "Microsoft", "Adobe", "Autodesk", "Corel"
        };

        return apps
            .Where(a => comPublishers.Any(p =>
                a.Publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .Select(a => a.Name)
            .ToList();
    }

    /// <summary>
    /// Finds the most likely main executable from a list of .exe files.
    /// </summary>
    internal static string? FindMainExecutable(string[] exeFiles, string appName)
    {
        if (exeFiles.Length == 0) return null;
        if (exeFiles.Length == 1) return exeFiles[0];

        // First: try to find an exe whose name matches the app name
        var nameMatch = exeFiles.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f)
                .Contains(appName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (nameMatch is not null) return nameMatch;

        // Second: filter out known non-main executables
        var nonMainPatterns = new[] { "unins", "update", "crash", "helper", "setup", "install" };
        var candidates = exeFiles
            .Where(f => !nonMainPatterns.Any(p =>
                Path.GetFileName(f).Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (candidates.Length == 0) candidates = exeFiles;

        // Third: pick the largest exe (heuristic — main exe tends to be largest)
        return candidates
            .OrderByDescending(f =>
            {
                try { return new FileInfo(f).Length; }
                catch { return 0L; }
            })
            .First();
    }

    private async Task CreateShortcutAsync(string shortcutPath, string targetPath, CancellationToken ct)
    {
        // Use PowerShell to create .lnk shortcut (avoids COM interop dependency)
        var script = $"""
            $ws = New-Object -ComObject WScript.Shell
            $sc = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}')
            $sc.TargetPath = '{targetPath.Replace("'", "''")}'
            $sc.WorkingDirectory = '{Path.GetDirectoryName(targetPath)?.Replace("'", "''")}'
            $sc.Save()
            """;

        await _processRunner.RunAsync("powershell",
            $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"", ct);
    }
}

/// <summary>
/// Tier 2 capture manifest with metadata about the captured apps.
/// </summary>
public class Tier2CaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<string> ApplicationNames { get; set; } = [];
    public List<string> LicensingWarnings { get; set; } = [];
    public List<string> ComRegistrations { get; set; } = [];
}
