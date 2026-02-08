using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers installed applications by scanning registry uninstall keys
/// and cross-referencing with winget and chocolatey package managers.
/// </summary>
public class ApplicationDiscoveryService
{
    private const string UninstallKey64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IRegistryAccessor _registry;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<ApplicationDiscoveryService> _logger;

    public ApplicationDiscoveryService(
        IRegistryAccessor registry,
        IFileSystemAccessor fileSystem,
        IProcessRunner processRunner,
        ILogger<ApplicationDiscoveryService> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all installed applications from registry uninstall keys.
    /// </summary>
    public async Task<List<DiscoveredApplication>> DiscoverAsync(CancellationToken ct = default)
    {
        var apps = new List<DiscoveredApplication>();

        // Scan HKLM 64-bit
        apps.AddRange(ScanUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, UninstallKey64, is32Bit: false, isPerUser: false));

        // Scan HKLM 32-bit (WOW6432Node)
        apps.AddRange(ScanUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry32, UninstallKey32, is32Bit: true, isPerUser: false));

        // Scan HKCU (per-user installs)
        apps.AddRange(ScanUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, UninstallKey64, is32Bit: false, isPerUser: true));

        // Deduplicate by name+version
        apps = DeduplicateApps(apps);

        // Cross-reference with package managers
        await EnrichWithWingetAsync(apps, ct);
        await EnrichWithChocolateyAsync(apps, ct);

        // Calculate sizes
        foreach (var app in apps)
        {
            app.EstimatedSizeBytes = CalculateAppSize(app);
        }

        _logger.LogInformation("Discovered {Count} installed applications", apps.Count);
        return apps;
    }

    private List<DiscoveredApplication> ScanUninstallKey(
        RegistryHive hive, RegistryView view, string keyPath, bool is32Bit, bool isPerUser)
    {
        var apps = new List<DiscoveredApplication>();

        string[] subKeyNames;
        try
        {
            subKeyNames = _registry.GetSubKeyNames(hive, view, keyPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read registry key {KeyPath}", keyPath);
            return apps;
        }

        foreach (var subKeyName in subKeyNames)
        {
            var fullPath = $@"{keyPath}\{subKeyName}";

            var displayName = _registry.GetStringValue(hive, view, fullPath, "DisplayName");
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            // Skip system components and updates
            var systemComponent = _registry.GetDwordValue(hive, view, fullPath, "SystemComponent");
            if (systemComponent == 1)
                continue;

            var parentKeyName = _registry.GetStringValue(hive, view, fullPath, "ParentKeyName");
            if (!string.IsNullOrEmpty(parentKeyName))
                continue; // This is an update/patch, not a standalone app

            var app = new DiscoveredApplication
            {
                Name = displayName.Trim(),
                Version = _registry.GetStringValue(hive, view, fullPath, "DisplayVersion") ?? string.Empty,
                Publisher = _registry.GetStringValue(hive, view, fullPath, "Publisher") ?? string.Empty,
                InstallLocation = _registry.GetStringValue(hive, view, fullPath, "InstallLocation"),
                UninstallString = _registry.GetStringValue(hive, view, fullPath, "UninstallString"),
                RegistryKeyPath = fullPath,
                Is32Bit = is32Bit,
                IsPerUser = isPerUser
            };

            // Try to find AppData paths
            DiscoverAppDataPaths(app);

            apps.Add(app);
        }

        return apps;
    }

    private void DiscoverAppDataPaths(DiscoveredApplication app)
    {
        // Look for app data in common locations based on publisher and name
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var searchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        searchNames.Add(app.Name);
        if (!string.IsNullOrEmpty(app.Publisher))
            searchNames.Add(app.Publisher);

        foreach (var basePath in new[] { appDataRoaming, appDataLocal })
        {
            if (string.IsNullOrEmpty(basePath)) continue;

            foreach (var name in searchNames)
            {
                var path = Path.Combine(basePath, name);
                if (_fileSystem.DirectoryExists(path))
                    app.AppDataPaths.Add(path);
            }
        }
    }

    private static List<DiscoveredApplication> DeduplicateApps(List<DiscoveredApplication> apps)
    {
        return apps
            .GroupBy(a => new { Name = a.Name.ToLowerInvariant(), a.Version })
            .Select(g => g.First())
            .ToList();
    }

    internal async Task EnrichWithWingetAsync(List<DiscoveredApplication> apps, CancellationToken ct)
    {
        try
        {
            var result = await _processRunner.RunAsync("winget", "list --disable-interactivity", ct);
            if (!result.Success)
            {
                _logger.LogWarning("winget list failed (exit code {ExitCode}), skipping winget enrichment", result.ExitCode);
                return;
            }

            var wingetApps = ParseWingetOutput(result.StandardOutput);

            foreach (var app in apps)
            {
                var match = wingetApps.FirstOrDefault(w =>
                    string.Equals(w.Name, app.Name, StringComparison.OrdinalIgnoreCase) ||
                    app.Name.Contains(w.Name, StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Contains(app.Name, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    app.WingetPackageId = match.PackageId;
                    _logger.LogDebug("Matched {AppName} to winget package {PackageId}", app.Name, match.PackageId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run winget, skipping package enrichment");
        }
    }

    internal async Task EnrichWithChocolateyAsync(List<DiscoveredApplication> apps, CancellationToken ct)
    {
        try
        {
            var result = await _processRunner.RunAsync("choco", "list --local-only --limit-output", ct);
            if (!result.Success)
            {
                _logger.LogWarning("choco list failed (exit code {ExitCode}), skipping chocolatey enrichment", result.ExitCode);
                return;
            }

            var chocoApps = ParseChocolateyOutput(result.StandardOutput);

            foreach (var app in apps)
            {
                var match = chocoApps.FirstOrDefault(c =>
                    string.Equals(c.Name, app.Name, StringComparison.OrdinalIgnoreCase) ||
                    app.Name.Contains(c.Name, StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Contains(app.Name, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    app.ChocolateyPackageId = match.PackageId;
                    _logger.LogDebug("Matched {AppName} to chocolatey package {PackageId}", app.Name, match.PackageId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run chocolatey, skipping package enrichment");
        }
    }

    internal static List<WingetEntry> ParseWingetOutput(string output)
    {
        var entries = new List<WingetEntry>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the separator line (all dashes) — column positions are determined by it
        int separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('-') && lines[i].Contains("--"))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 1) return entries;

        // Use the header line (line before separator) to find column positions
        var headerLine = lines[separatorIndex - 1];
        int idCol = headerLine.IndexOf("Id", StringComparison.Ordinal);
        if (idCol < 0) return entries;

        // Parse data lines after the separator
        for (int i = separatorIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length <= idCol) continue;

            var name = line[..idCol].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var rest = line[idCol..].Trim();
            var idParts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (idParts.Length == 0) continue;

            entries.Add(new WingetEntry
            {
                Name = name,
                PackageId = idParts[0]
            });
        }

        return entries;
    }

    internal static List<ChocolateyEntry> ParseChocolateyOutput(string output)
    {
        var entries = new List<ChocolateyEntry>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Chocolatey --limit-output format: packageId|version
            var parts = line.Trim().Split('|');
            if (parts.Length >= 2)
            {
                entries.Add(new ChocolateyEntry
                {
                    Name = parts[0].Trim(),
                    PackageId = parts[0].Trim(),
                    Version = parts[1].Trim()
                });
            }
        }

        return entries;
    }

    private long CalculateAppSize(DiscoveredApplication app)
    {
        long total = 0;

        // Size from install location
        if (!string.IsNullOrEmpty(app.InstallLocation) && _fileSystem.DirectoryExists(app.InstallLocation))
        {
            total += _fileSystem.GetDirectorySize(app.InstallLocation);
        }

        // Size from AppData paths
        foreach (var path in app.AppDataPaths)
        {
            total += _fileSystem.GetDirectorySize(path);
        }

        // Fallback: use registry EstimatedSize (in KB)
        if (total == 0)
        {
            var estimatedKb = _registry.GetDwordValue(
                app.IsPerUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
                app.Is32Bit ? RegistryView.Registry32 : RegistryView.Registry64,
                app.RegistryKeyPath,
                "EstimatedSize");

            if (estimatedKb.HasValue)
                total = estimatedKb.Value * 1024L;
        }

        return total;
    }
}

internal class WingetEntry
{
    public string Name { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
}

internal class ChocolateyEntry
{
    public string Name { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
