using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and restores application-specific registry keys for Tier 2 migration.
/// Uses reg.exe for export/import and filters hardware-specific keys.
/// </summary>
public class RegistryCaptureService
{
    private const string RegistryManifestFileName = "registry-manifest.json";

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<RegistryCaptureService> _logger;

    /// <summary>
    /// Registry key path prefixes that should be filtered out during capture
    /// because they are hardware-specific and shouldn't transfer between machines.
    /// </summary>
    internal static readonly string[] HardwareKeyFilters =
    [
        @"SYSTEM\CurrentControlSet\Enum",
        @"SYSTEM\CurrentControlSet\Control\Class",
        @"SYSTEM\CurrentControlSet\Services\disk",
        @"SYSTEM\CurrentControlSet\Services\volmgr",
        @"SYSTEM\CurrentControlSet\Services\partmgr",
        @"SYSTEM\CurrentControlSet\Services\USBSTOR",
        @"SYSTEM\MountedDevices",
        @"HARDWARE\",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate",
        @"SOFTWARE\Microsoft\Cryptography\MachineGuid"
    ];

    public RegistryCaptureService(
        IProcessRunner processRunner,
        ILogger<RegistryCaptureService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Exports registry keys associated with the given applications.
    /// </summary>
    /// <param name="apps">Applications to capture registry for.</param>
    /// <param name="outputDir">Directory to write registry export files.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Manifest describing all exported registry files.</returns>
    public async Task<RegistryCaptureManifest> ExportAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        string outputDir,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var manifest = new RegistryCaptureManifest();
        int fileIndex = 0;

        foreach (var app in apps)
        {
            ct.ThrowIfCancellationRequested();

            var keysToExport = BuildKeyListForApp(app);

            foreach (var key in keysToExport)
            {
                if (IsHardwareKey(key))
                {
                    _logger.LogDebug("Skipping hardware-specific key: {Key}", key);
                    continue;
                }

                var exportFileName = $"reg-{fileIndex:D4}.reg";
                var exportPath = Path.Combine(outputDir, exportFileName);

                try
                {
                    var result = await _processRunner.RunAsync(
                        "reg", $"export \"{key}\" \"{exportPath}\" /y", ct);

                    if (result.Success)
                    {
                        manifest.Entries.Add(new RegistryExportEntry
                        {
                            ApplicationName = app.Name,
                            RegistryKeyPath = key,
                            ExportFileName = exportFileName,
                            IsHklm = key.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
                                     key.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                        });

                        _logger.LogDebug("Exported registry key: {Key} -> {File}", key, exportFileName);
                        fileIndex++;
                    }
                    else
                    {
                        _logger.LogDebug("Registry key not found or empty: {Key}", key);
                        // Clean up empty file if reg.exe created one
                        if (File.Exists(exportPath))
                            File.Delete(exportPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to export registry key {Key}", key);
                }
            }
        }

        // Write manifest
        var manifestPath = Path.Combine(outputDir, RegistryManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Exported {Count} registry keys for {AppCount} apps",
            manifest.Entries.Count, apps.Count);

        return manifest;
    }

    /// <summary>
    /// Imports previously exported registry keys to the destination machine.
    /// Applies user path remapping to .reg file contents before importing.
    /// </summary>
    public async Task ImportAsync(
        string inputDir,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(inputDir, RegistryManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("No registry manifest found at {Path}, skipping import", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<RegistryCaptureManifest>(json);
        if (manifest is null) return;

        foreach (var entry in manifest.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var regFile = Path.Combine(inputDir, entry.ExportFileName);
            if (!File.Exists(regFile))
            {
                _logger.LogWarning("Registry export file not found: {File}", entry.ExportFileName);
                continue;
            }

            // Apply user path remapping to the .reg file content
            var importFile = regFile;
            if (userMappings.Any(m => m.RequiresPathRemapping))
            {
                importFile = await RemapRegFilePathsAsync(regFile, userMappings, ct);
            }

            try
            {
                var result = await _processRunner.RunAsync(
                    "reg", $"import \"{importFile}\"", ct);

                if (result.Success)
                {
                    _logger.LogDebug("Imported registry key: {Key}", entry.RegistryKeyPath);
                }
                else
                {
                    _logger.LogWarning("Failed to import {Key}: {Error}",
                        entry.RegistryKeyPath, result.StandardError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import registry file {File}", entry.ExportFileName);
            }
            finally
            {
                // Clean up temporary remapped file
                if (importFile != regFile && File.Exists(importFile))
                    File.Delete(importFile);
            }
        }

        _logger.LogInformation("Registry import complete ({Count} entries)", manifest.Entries.Count);
    }

    /// <summary>
    /// Builds the list of registry keys to export for a given app.
    /// </summary>
    internal static List<string> BuildKeyListForApp(DiscoveredApplication app)
    {
        var keys = new List<string>();

        // 1. The app's uninstall registry key
        if (!string.IsNullOrEmpty(app.RegistryKeyPath))
        {
            var hive = app.IsPerUser ? "HKCU" : "HKLM";
            keys.Add($@"{hive}\{app.RegistryKeyPath}");
        }

        // 2. HKLM\SOFTWARE\{Publisher}\{AppName} and HKLM\SOFTWARE\{AppName}
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(app.Name)) names.Add(app.Name);
        if (!string.IsNullOrEmpty(app.Publisher)) names.Add(app.Publisher);

        foreach (var name in names)
        {
            keys.Add($@"HKLM\SOFTWARE\{name}");
            keys.Add($@"HKCU\Software\{name}");
        }

        // 3. For 32-bit apps, also check WOW6432Node
        if (app.Is32Bit)
        {
            foreach (var name in names)
            {
                keys.Add($@"HKLM\SOFTWARE\WOW6432Node\{name}");
            }
        }

        // 4. Publisher\AppName combination
        if (!string.IsNullOrEmpty(app.Publisher) && !string.IsNullOrEmpty(app.Name))
        {
            keys.Add($@"HKLM\SOFTWARE\{app.Publisher}\{app.Name}");
            keys.Add($@"HKCU\Software\{app.Publisher}\{app.Name}");
        }

        // 5. Additional paths explicitly specified on the app
        keys.AddRange(app.AdditionalRegistryPaths);

        // Deduplicate
        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Checks if a registry key path is hardware-specific and should be filtered out.
    /// </summary>
    internal static bool IsHardwareKey(string keyPath)
    {
        // Strip the hive prefix (HKLM\, HKCU\, etc.)
        var stripped = keyPath;
        var backslashIdx = keyPath.IndexOf('\\');
        if (backslashIdx >= 0)
            stripped = keyPath[(backslashIdx + 1)..];

        return HardwareKeyFilters.Any(filter =>
            stripped.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Rewrites user profile paths inside a .reg file for user path remapping.
    /// </summary>
    internal async Task<string> RemapRegFilePathsAsync(
        string regFilePath,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(regFilePath, ct);
        var modified = false;

        foreach (var mapping in userMappings)
        {
            if (!mapping.RequiresPathRemapping) continue;

            var sourcePrefix = mapping.SourcePathPrefix;
            var destPrefix = mapping.DestinationProfilePath;

            if (string.IsNullOrEmpty(sourcePrefix) || string.IsNullOrEmpty(destPrefix))
                continue;

            // In .reg files, backslashes in values are doubled (\\)
            var sourceDoubled = sourcePrefix.Replace("\\", "\\\\");
            var destDoubled = destPrefix.Replace("\\", "\\\\");

            if (content.Contains(sourceDoubled, StringComparison.OrdinalIgnoreCase))
            {
                content = content.Replace(sourceDoubled, destDoubled, StringComparison.OrdinalIgnoreCase);
                modified = true;
            }

            // Also handle single-backslash paths (some value types)
            if (content.Contains(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                content = content.Replace(sourcePrefix, destPrefix, StringComparison.OrdinalIgnoreCase);
                modified = true;
            }
        }

        if (!modified) return regFilePath;

        var tempFile = Path.Combine(Path.GetTempPath(), $"zim-remap-{Guid.NewGuid():N}.reg");
        await File.WriteAllTextAsync(tempFile, content, ct);
        return tempFile;
    }
}

/// <summary>
/// Manifest describing all exported registry files for Tier 2 migration.
/// </summary>
public class RegistryCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<RegistryExportEntry> Entries { get; set; } = [];
}

/// <summary>
/// A single exported registry key file.
/// </summary>
public class RegistryExportEntry
{
    public string ApplicationName { get; set; } = string.Empty;
    public string RegistryKeyPath { get; set; } = string.Empty;
    public string ExportFileName { get; set; } = string.Empty;
    public bool IsHklm { get; set; }
}
