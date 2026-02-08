using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers transferable system settings: printers, WiFi, mapped drives,
/// environment variables, scheduled tasks, credentials, certificates, default app associations.
/// </summary>
public class SystemSettingsDiscoveryService
{
    private readonly IRegistryAccessor _registry;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<SystemSettingsDiscoveryService> _logger;

    public SystemSettingsDiscoveryService(
        IRegistryAccessor registry,
        IProcessRunner processRunner,
        ILogger<SystemSettingsDiscoveryService> logger)
    {
        _registry = registry;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all transferable system settings.
    /// </summary>
    public async Task<List<SystemSetting>> DiscoverAsync(CancellationToken ct = default)
    {
        var settings = new List<SystemSetting>();

        settings.AddRange(DiscoverPrinters());
        settings.AddRange(await DiscoverWifiProfilesAsync(ct));
        settings.AddRange(DiscoverMappedDrives());
        settings.AddRange(DiscoverEnvironmentVariables());
        settings.AddRange(await DiscoverScheduledTasksAsync(ct));
        settings.AddRange(DiscoverDefaultAppAssociations());

        _logger.LogInformation("Discovered {Count} system settings", settings.Count);
        return settings;
    }

    internal List<SystemSetting> DiscoverPrinters()
    {
        var printers = new List<SystemSetting>();

        try
        {
            var printerNames = _registry.GetSubKeyNames(
                RegistryHive.CurrentUser, RegistryView.Default,
                @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts");

            foreach (var name in printerNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                printers.Add(new SystemSetting
                {
                    Name = name,
                    Description = $"Printer: {name}",
                    Category = SystemSettingCategory.Printer,
                    Data = _registry.GetStringValue(
                        RegistryHive.CurrentUser, RegistryView.Default,
                        @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts", name)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover printers");
        }

        return printers;
    }

    internal async Task<List<SystemSetting>> DiscoverWifiProfilesAsync(CancellationToken ct)
    {
        var profiles = new List<SystemSetting>();

        try
        {
            var result = await _processRunner.RunAsync("netsh", "wlan show profiles", ct);
            if (!result.Success) return profiles;

            foreach (var line in result.StandardOutput.Split('\n'))
            {
                // Lines look like: "    All User Profile     : MyWiFiNetwork"
                var trimmed = line.Trim();
                if (!trimmed.Contains(":", StringComparison.Ordinal)) continue;

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0 || !trimmed.Contains("Profile", StringComparison.OrdinalIgnoreCase))
                    continue;

                var profileName = trimmed[(colonIndex + 1)..].Trim();
                if (string.IsNullOrEmpty(profileName)) continue;

                // Export the profile XML
                var exportResult = await _processRunner.RunAsync(
                    "netsh", $"wlan show profile name=\"{profileName}\" key=clear", ct);

                profiles.Add(new SystemSetting
                {
                    Name = profileName,
                    Description = $"WiFi network: {profileName}",
                    Category = SystemSettingCategory.WifiProfile,
                    Data = exportResult.Success ? exportResult.StandardOutput : null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover WiFi profiles");
        }

        return profiles;
    }

    internal List<SystemSetting> DiscoverMappedDrives()
    {
        var drives = new List<SystemSetting>();

        try
        {
            var driveLetters = _registry.GetValueNames(
                RegistryHive.CurrentUser, RegistryView.Default,
                @"Network");

            // Each subkey under HKCU\Network is a drive letter
            var subKeys = _registry.GetSubKeyNames(
                RegistryHive.CurrentUser, RegistryView.Default,
                @"Network");

            foreach (var driveLetter in subKeys)
            {
                var remotePath = _registry.GetStringValue(
                    RegistryHive.CurrentUser, RegistryView.Default,
                    $@"Network\{driveLetter}", "RemotePath");

                if (string.IsNullOrEmpty(remotePath)) continue;

                drives.Add(new SystemSetting
                {
                    Name = $"{driveLetter}: → {remotePath}",
                    Description = $"Mapped drive {driveLetter}: to {remotePath}",
                    Category = SystemSettingCategory.MappedDrive,
                    Data = remotePath
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover mapped drives");
        }

        return drives;
    }

    internal List<SystemSetting> DiscoverEnvironmentVariables()
    {
        var envVars = new List<SystemSetting>();

        try
        {
            // User environment variables
            var userVarNames = _registry.GetValueNames(
                RegistryHive.CurrentUser, RegistryView.Default,
                @"Environment");

            foreach (var name in userVarNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                var value = _registry.GetStringValue(
                    RegistryHive.CurrentUser, RegistryView.Default,
                    @"Environment", name);

                envVars.Add(new SystemSetting
                {
                    Name = $"{name} (User)",
                    Description = $"User environment variable: {name}={value}",
                    Category = SystemSettingCategory.EnvironmentVariable,
                    Data = $"{name}={value}"
                });
            }

            // System environment variables
            var sysVarNames = _registry.GetValueNames(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");

            foreach (var name in sysVarNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Skip standard system vars that shouldn't be transferred
                if (IsStandardSystemEnvVar(name)) continue;

                var value = _registry.GetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", name);

                envVars.Add(new SystemSetting
                {
                    Name = $"{name} (System)",
                    Description = $"System environment variable: {name}={value}",
                    Category = SystemSettingCategory.EnvironmentVariable,
                    Data = $"{name}={value}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover environment variables");
        }

        return envVars;
    }

    private static bool IsStandardSystemEnvVar(string name)
    {
        var standard = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ComSpec", "OS", "Path", "PATHEXT", "PROCESSOR_ARCHITECTURE",
            "PROCESSOR_IDENTIFIER", "PROCESSOR_LEVEL", "PROCESSOR_REVISION",
            "PSModulePath", "SystemDrive", "SystemRoot", "TEMP", "TMP",
            "USERNAME", "windir", "NUMBER_OF_PROCESSORS", "DriverData"
        };
        return standard.Contains(name);
    }

    internal async Task<List<SystemSetting>> DiscoverScheduledTasksAsync(CancellationToken ct)
    {
        var tasks = new List<SystemSetting>();

        try
        {
            // Use schtasks to list user-created tasks
            var result = await _processRunner.RunAsync(
                "schtasks", "/query /fo CSV /nh /v", ct);

            if (!result.Success) return tasks;

            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = ParseCsvLine(line);
                if (fields.Count < 2) continue;

                var taskName = fields[0].Trim('"');
                // Skip Microsoft/system tasks
                if (taskName.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
                if (taskName.StartsWith(@"\Windows", StringComparison.OrdinalIgnoreCase)) continue;

                tasks.Add(new SystemSetting
                {
                    Name = taskName,
                    Description = $"Scheduled task: {taskName}",
                    Category = SystemSettingCategory.ScheduledTask,
                    Data = line
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover scheduled tasks");
        }

        return tasks;
    }

    internal List<SystemSetting> DiscoverDefaultAppAssociations()
    {
        var assocs = new List<SystemSetting>();

        try
        {
            // Read user-specific file associations from HKCU
            var extensions = _registry.GetSubKeyNames(
                RegistryHive.CurrentUser, RegistryView.Default,
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts");

            var count = 0;
            foreach (var ext in extensions)
            {
                if (!ext.StartsWith('.')) continue;

                var userChoice = _registry.GetStringValue(
                    RegistryHive.CurrentUser, RegistryView.Default,
                    $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice",
                    "ProgId");

                if (string.IsNullOrEmpty(userChoice)) continue;

                count++;
            }

            if (count > 0)
            {
                assocs.Add(new SystemSetting
                {
                    Name = $"Default App Associations ({count} extensions)",
                    Description = $"{count} custom file type associations",
                    Category = SystemSettingCategory.DefaultAppAssociation,
                    Data = count.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover default app associations");
        }

        return assocs;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
