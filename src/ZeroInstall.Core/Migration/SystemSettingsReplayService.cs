using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and replays system settings: WiFi profiles, printers, mapped drives,
/// environment variables, scheduled tasks, default app associations, credentials, certificates.
/// </summary>
public class SystemSettingsReplayService
{
    private const string ManifestFileName = "system-settings-manifest.json";

    private readonly IProcessRunner _processRunner;
    private readonly IRegistryAccessor _registry;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger<SystemSettingsReplayService> _logger;

    public SystemSettingsReplayService(
        IProcessRunner processRunner,
        IRegistryAccessor registry,
        IFileSystemAccessor fileSystem,
        ILogger<SystemSettingsReplayService> logger)
    {
        _processRunner = processRunner;
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Captures system settings for selected items and writes a manifest.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var settingsItems = items
            .Where(i => i.IsSelected && i.ItemType == MigrationItemType.SystemSetting)
            .ToList();

        if (settingsItems.Count == 0) return;

        Directory.CreateDirectory(outputPath);
        var manifest = new SystemSettingsCaptureManifest();

        foreach (var item in settingsItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.SourceData is not SystemSetting setting) continue;

            item.Status = MigrationItemStatus.InProgress;
            statusProgress?.Report($"Capturing {setting.Category}: {setting.Name}");

            var entry = await CaptureSettingAsync(setting, outputPath, ct);
            manifest.Settings.Add(entry);

            item.Status = entry.Status == CapturedSettingStatus.Error
                ? MigrationItemStatus.Failed
                : MigrationItemStatus.Completed;
        }

        manifest.CapturedUtc = DateTime.UtcNow;

        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Captured {Count} system settings to {Path}",
            manifest.Settings.Count, outputPath);
    }

    /// <summary>
    /// Restores system settings from a previous capture.
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(inputPath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("No system settings manifest found at {Path}", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<SystemSettingsCaptureManifest>(json);
        if (manifest is null) return;

        foreach (var entry in manifest.Settings)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Status == CapturedSettingStatus.Warning)
            {
                _logger.LogWarning("Skipping {Category} '{Name}': {Message}",
                    entry.Category, entry.Name, entry.StatusMessage);
                statusProgress?.Report($"Skipped (manual): {entry.Name}");
                continue;
            }

            statusProgress?.Report($"Restoring {entry.Category}: {entry.Name}");

            await RestoreSettingAsync(entry, inputPath, userMappings, ct);
        }

        _logger.LogInformation("Restored system settings from {Path}", inputPath);
    }

    private async Task<CapturedSettingEntry> CaptureSettingAsync(
        SystemSetting setting, string outputPath, CancellationToken ct)
    {
        var entry = new CapturedSettingEntry
        {
            Name = setting.Name,
            Category = setting.Category,
            Data = setting.Data
        };

        try
        {
            switch (setting.Category)
            {
                case SystemSettingCategory.WifiProfile:
                    await CaptureWifiProfileAsync(setting, outputPath, entry, ct);
                    break;

                case SystemSettingCategory.Printer:
                    CapturePrinter(setting, entry);
                    break;

                case SystemSettingCategory.MappedDrive:
                    CaptureMappedDrive(setting, entry);
                    break;

                case SystemSettingCategory.EnvironmentVariable:
                    CaptureEnvironmentVariable(setting, entry);
                    break;

                case SystemSettingCategory.ScheduledTask:
                    await CaptureScheduledTaskAsync(setting, outputPath, entry, ct);
                    break;

                case SystemSettingCategory.DefaultAppAssociation:
                    await CaptureDefaultAppsAsync(outputPath, entry, ct);
                    break;

                case SystemSettingCategory.Credential:
                    entry.Status = CapturedSettingStatus.Warning;
                    entry.StatusMessage = "Credentials cannot be exported — user must re-enter manually";
                    break;

                case SystemSettingCategory.Certificate:
                    entry.Status = CapturedSettingStatus.Warning;
                    entry.StatusMessage = "Certificates require manual export/import with private keys";
                    break;

                default:
                    entry.Status = CapturedSettingStatus.Warning;
                    entry.StatusMessage = $"Unknown setting category: {setting.Category}";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture {Category}: {Name}", setting.Category, setting.Name);
            entry.Status = CapturedSettingStatus.Error;
            entry.StatusMessage = ex.Message;
        }

        return entry;
    }

    private async Task CaptureWifiProfileAsync(
        SystemSetting setting, string outputPath, CapturedSettingEntry entry, CancellationToken ct)
    {
        var wifiDir = Path.Combine(outputPath, "wifi");
        Directory.CreateDirectory(wifiDir);

        var result = await _processRunner.RunAsync(
            "netsh", $"wlan export profile name=\"{setting.Name}\" key=clear folder=\"{wifiDir}\"", ct);

        if (result.Success)
        {
            entry.Status = CapturedSettingStatus.Captured;
            entry.CaptureFileName = $"wifi/{setting.Name}.xml";
            _logger.LogDebug("Exported WiFi profile: {Name}", setting.Name);
        }
        else
        {
            entry.Status = CapturedSettingStatus.Error;
            entry.StatusMessage = result.StandardError;
        }
    }

    private static void CapturePrinter(SystemSetting setting, CapturedSettingEntry entry)
    {
        entry.Status = CapturedSettingStatus.Captured;
        // Data contains printer name and connection info
    }

    private static void CaptureMappedDrive(SystemSetting setting, CapturedSettingEntry entry)
    {
        entry.Status = CapturedSettingStatus.Captured;
        // Data contains drive letter and UNC path
    }

    private static void CaptureEnvironmentVariable(SystemSetting setting, CapturedSettingEntry entry)
    {
        entry.Status = CapturedSettingStatus.Captured;
        // Data contains name=value
    }

    private async Task CaptureScheduledTaskAsync(
        SystemSetting setting, string outputPath, CapturedSettingEntry entry, CancellationToken ct)
    {
        var tasksDir = Path.Combine(outputPath, "tasks");
        Directory.CreateDirectory(tasksDir);

        var safeName = setting.Name.Replace("\\", "_").Replace("/", "_");
        var xmlFile = Path.Combine(tasksDir, $"{safeName}.xml");

        var result = await _processRunner.RunAsync(
            "schtasks", $"/query /xml /tn \"{setting.Name}\"", ct);

        if (result.Success)
        {
            await File.WriteAllTextAsync(xmlFile, result.StandardOutput, ct);
            entry.Status = CapturedSettingStatus.Captured;
            entry.CaptureFileName = $"tasks/{safeName}.xml";
            _logger.LogDebug("Exported scheduled task: {Name}", setting.Name);
        }
        else
        {
            entry.Status = CapturedSettingStatus.Error;
            entry.StatusMessage = result.StandardError;
        }
    }

    private async Task CaptureDefaultAppsAsync(
        string outputPath, CapturedSettingEntry entry, CancellationToken ct)
    {
        var file = Path.Combine(outputPath, "default-apps.xml");

        var result = await _processRunner.RunAsync(
            "dism", $"/online /export-defaultappassociations:\"{file}\"", ct);

        if (result.Success)
        {
            entry.Status = CapturedSettingStatus.Captured;
            entry.CaptureFileName = "default-apps.xml";
        }
        else
        {
            entry.Status = CapturedSettingStatus.Error;
            entry.StatusMessage = result.StandardError;
        }
    }

    private async Task RestoreSettingAsync(
        CapturedSettingEntry entry, string inputPath,
        IReadOnlyList<UserMapping> userMappings, CancellationToken ct)
    {
        try
        {
            switch (entry.Category)
            {
                case SystemSettingCategory.WifiProfile:
                    await RestoreWifiProfileAsync(entry, inputPath, ct);
                    break;

                case SystemSettingCategory.Printer:
                    await RestorePrinterAsync(entry, ct);
                    break;

                case SystemSettingCategory.MappedDrive:
                    await RestoreMappedDriveAsync(entry, ct);
                    break;

                case SystemSettingCategory.EnvironmentVariable:
                    await RestoreEnvironmentVariableAsync(entry, userMappings, ct);
                    break;

                case SystemSettingCategory.ScheduledTask:
                    await RestoreScheduledTaskAsync(entry, inputPath, ct);
                    break;

                case SystemSettingCategory.DefaultAppAssociation:
                    await RestoreDefaultAppsAsync(entry, inputPath, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore {Category}: {Name}", entry.Category, entry.Name);
        }
    }

    private async Task RestoreWifiProfileAsync(
        CapturedSettingEntry entry, string inputPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.CaptureFileName)) return;

        // Find the actual exported XML file in the wifi directory
        var wifiDir = Path.Combine(inputPath, "wifi");
        if (!Directory.Exists(wifiDir)) return;

        var xmlFiles = Directory.GetFiles(wifiDir, "*.xml");
        var xmlFile = xmlFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Contains(entry.Name, StringComparison.OrdinalIgnoreCase))
            ?? xmlFiles.FirstOrDefault();

        if (xmlFile is null) return;

        var result = await _processRunner.RunAsync(
            "netsh", $"wlan add profile filename=\"{xmlFile}\" user=all", ct);

        if (result.Success)
            _logger.LogDebug("Restored WiFi profile: {Name}", entry.Name);
        else
            _logger.LogWarning("Failed to restore WiFi profile {Name}: {Error}", entry.Name, result.StandardError);
    }

    internal async Task RestorePrinterAsync(CapturedSettingEntry entry, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.Data)) return;

        // Network printers can be auto-added; local printers need manual setup
        if (entry.Data.StartsWith("\\\\", StringComparison.Ordinal))
        {
            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"Add-Printer -ConnectionName '{entry.Data}'\"", ct);

            if (result.Success)
                _logger.LogDebug("Restored network printer: {Name}", entry.Name);
            else
                _logger.LogWarning("Failed to restore printer {Name}: {Error}", entry.Name, result.StandardError);
        }
        else
        {
            _logger.LogWarning("Local printer '{Name}' requires manual driver installation", entry.Name);
        }
    }

    internal async Task RestoreMappedDriveAsync(CapturedSettingEntry entry, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.Data)) return;

        // Data format: "X:|\\server\share"
        var parts = entry.Data.Split('|', 2);
        if (parts.Length != 2) return;

        var driveLetter = parts[0];
        var uncPath = parts[1];

        var result = await _processRunner.RunAsync(
            "net", $"use {driveLetter} \"{uncPath}\" /persistent:yes", ct);

        if (result.Success)
            _logger.LogDebug("Restored mapped drive {Letter} → {Path}", driveLetter, uncPath);
        else
            _logger.LogWarning("Failed to restore mapped drive {Letter}: {Error}", driveLetter, result.StandardError);
    }

    internal async Task RestoreEnvironmentVariableAsync(
        CapturedSettingEntry entry, IReadOnlyList<UserMapping> userMappings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.Data)) return;

        // Data format: "name=value" with optional "|system" suffix
        var isSystem = entry.Data.EndsWith("|system", StringComparison.OrdinalIgnoreCase);
        var data = isSystem ? entry.Data[..^"|system".Length] : entry.Data;

        var eqIdx = data.IndexOf('=');
        if (eqIdx < 0) return;

        var name = data[..eqIdx];
        var value = data[(eqIdx + 1)..];

        // Apply user path remapping
        value = RemapEnvironmentValue(value, userMappings);

        var args = isSystem
            ? $"\"{name}\" \"{value}\" /M"
            : $"\"{name}\" \"{value}\"";

        var result = await _processRunner.RunAsync("setx", args, ct);

        if (result.Success)
            _logger.LogDebug("Restored environment variable: {Name}", name);
        else
            _logger.LogWarning("Failed to restore env var {Name}: {Error}", name, result.StandardError);
    }

    private async Task RestoreScheduledTaskAsync(
        CapturedSettingEntry entry, string inputPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.CaptureFileName)) return;

        var xmlFile = Path.Combine(inputPath, entry.CaptureFileName);
        if (!File.Exists(xmlFile)) return;

        var result = await _processRunner.RunAsync(
            "schtasks", $"/create /xml \"{xmlFile}\" /tn \"{entry.Name}\" /f", ct);

        if (result.Success)
            _logger.LogDebug("Restored scheduled task: {Name}", entry.Name);
        else
            _logger.LogWarning("Failed to restore task {Name}: {Error}", entry.Name, result.StandardError);
    }

    private async Task RestoreDefaultAppsAsync(
        CapturedSettingEntry entry, string inputPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.CaptureFileName)) return;

        var file = Path.Combine(inputPath, entry.CaptureFileName);
        if (!File.Exists(file)) return;

        var result = await _processRunner.RunAsync(
            "dism", $"/online /import-defaultappassociations:\"{file}\"", ct);

        if (result.Success)
            _logger.LogDebug("Restored default app associations");
        else
            _logger.LogWarning("Failed to restore default app associations: {Error}", result.StandardError);
    }

    internal static string RemapEnvironmentValue(
        string value, IReadOnlyList<UserMapping> userMappings)
    {
        foreach (var mapping in userMappings)
        {
            if (!mapping.RequiresPathRemapping) continue;

            var src = mapping.SourcePathPrefix;
            var dest = mapping.DestinationProfilePath;

            if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dest) &&
                value.Contains(src, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Replace(src, dest, StringComparison.OrdinalIgnoreCase);
            }
        }

        return value;
    }
}

/// <summary>
/// Manifest tracking captured system settings.
/// </summary>
public class SystemSettingsCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<CapturedSettingEntry> Settings { get; set; } = [];
}

/// <summary>
/// A captured system setting entry.
/// </summary>
public class CapturedSettingEntry
{
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public SystemSettingCategory Category { get; set; }

    public string? Data { get; set; }
    public string? CaptureFileName { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public CapturedSettingStatus Status { get; set; } = CapturedSettingStatus.Captured;

    public string? StatusMessage { get; set; }
}

public enum CapturedSettingStatus
{
    Captured,
    Warning,
    Error
}
