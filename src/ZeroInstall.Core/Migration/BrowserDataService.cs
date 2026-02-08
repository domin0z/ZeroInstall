using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and restores browser data (bookmarks, extensions, settings, profile folders)
/// for Chrome, Firefox, and Edge.
/// </summary>
public class BrowserDataService
{
    private const string ManifestFileName = "browser-manifest.json";

    private static readonly string[] ChromeExcludePatterns =
        ["Cache", "Code Cache", "GPUCache", "Service Worker", "DawnCache", "GrShaderCache"];

    private static readonly string[] FirefoxExcludePatterns =
        ["cache2", "startupCache", "shader-cache"];

    private readonly IFileSystemAccessor _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<BrowserDataService> _logger;

    public BrowserDataService(
        IFileSystemAccessor fileSystem,
        IProcessRunner processRunner,
        ILogger<BrowserDataService> logger)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Captures browser data for selected items.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var browserItems = items
            .Where(i => i.IsSelected && i.ItemType == MigrationItemType.BrowserData)
            .ToList();

        if (browserItems.Count == 0) return;

        Directory.CreateDirectory(outputPath);
        var manifest = new BrowserCaptureManifest();

        foreach (var item in browserItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.SourceData is not UserProfile profile) continue;

            item.Status = MigrationItemStatus.InProgress;

            foreach (var browser in profile.BrowserProfiles)
            {
                statusProgress?.Report($"Capturing {browser.BrowserName} data for {profile.Username}");

                var entry = await CaptureBrowserProfileAsync(
                    browser, profile.Username, outputPath, ct);
                if (entry is not null)
                    manifest.Browsers.Add(entry);
            }

            item.Status = MigrationItemStatus.Completed;
        }

        manifest.CapturedUtc = DateTime.UtcNow;

        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Captured {Count} browser profiles to {Path}",
            manifest.Browsers.Count, outputPath);
    }

    /// <summary>
    /// Restores previously captured browser data, applying user mappings.
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
            _logger.LogWarning("No browser manifest found at {Path}", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<BrowserCaptureManifest>(json);
        if (manifest is null) return;

        foreach (var entry in manifest.Browsers)
        {
            ct.ThrowIfCancellationRequested();

            var mapping = userMappings.FirstOrDefault(m =>
                string.Equals(m.SourceUser.Username, entry.SourceUsername, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                _logger.LogWarning("No user mapping for browser profile owner {User}", entry.SourceUsername);
                continue;
            }

            statusProgress?.Report($"Restoring {entry.BrowserName} for {mapping.DestinationUsername}");

            var capturedDir = Path.Combine(inputPath, entry.CaptureSubDir);
            if (!Directory.Exists(capturedDir))
            {
                _logger.LogWarning("Captured browser data not found: {Dir}", capturedDir);
                continue;
            }

            var destPath = GetDestinationBrowserPath(
                entry.BrowserName, entry.ProfileName, mapping);

            if (destPath is null)
            {
                _logger.LogWarning("Cannot determine destination path for {Browser}", entry.BrowserName);
                continue;
            }

            ProfileTransferService.CopyDirectoryWithTimestamps(capturedDir, destPath);

            // Update Firefox profiles.ini if needed
            if (string.Equals(entry.BrowserName, "Firefox", StringComparison.OrdinalIgnoreCase))
            {
                await UpdateFirefoxProfilesIniAsync(destPath, entry.ProfileName, mapping, ct);
            }

            _logger.LogDebug("Restored {Browser} profile {Profile} for {User}",
                entry.BrowserName, entry.ProfileName, mapping.DestinationUsername);
        }

        _logger.LogInformation("Restored {Count} browser profiles from {Path}",
            manifest.Browsers.Count, inputPath);
    }

    private async Task<CapturedBrowserEntry?> CaptureBrowserProfileAsync(
        BrowserProfile browser, string username, string outputPath, CancellationToken ct)
    {
        if (!_fileSystem.DirectoryExists(browser.ProfilePath))
        {
            _logger.LogDebug("Browser profile path does not exist: {Path}", browser.ProfilePath);
            return null;
        }

        var captureSubDir = $"{username}_{browser.BrowserName}_{browser.ProfileName}"
            .Replace(" ", "-");
        var destDir = Path.Combine(outputPath, captureSubDir);

        try
        {
            var excludePatterns = GetExcludePatternsForBrowser(browser.BrowserName);
            CopyDirectorySelective(browser.ProfilePath, destDir, excludePatterns);

            // Log DPAPI warning for Chromium-based browsers
            if (IsChromiumBased(browser.BrowserName))
            {
                _logger.LogWarning(
                    "Browser {Browser}: saved passwords are DPAPI-encrypted and may not be transferable to a different machine",
                    browser.BrowserName);
            }

            await Task.CompletedTask;

            return new CapturedBrowserEntry
            {
                BrowserName = browser.BrowserName,
                ProfileName = browser.ProfileName,
                SourceUsername = username,
                SourcePath = browser.ProfilePath,
                CaptureSubDir = captureSubDir
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture {Browser} profile for {User}",
                browser.BrowserName, username);
            return null;
        }
    }

    internal static void CopyDirectorySelective(
        string sourceDir, string destDir, string[] excludePatterns)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);

            if (excludePatterns.Any(p =>
                string.Equals(dirName, p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectorySelective(dir, destSubDir, excludePatterns);
        }
    }

    internal static string? GetDestinationBrowserPath(
        string browserName, string profileName, UserMapping mapping)
    {
        var destProfile = mapping.DestinationProfilePath;
        if (string.IsNullOrEmpty(destProfile)) return null;

        return browserName.ToLowerInvariant() switch
        {
            "chrome" or "google chrome" =>
                Path.Combine(destProfile, "AppData", "Local", "Google", "Chrome", "User Data", profileName),
            "edge" or "microsoft edge" =>
                Path.Combine(destProfile, "AppData", "Local", "Microsoft", "Edge", "User Data", profileName),
            "firefox" or "mozilla firefox" =>
                Path.Combine(destProfile, "AppData", "Roaming", "Mozilla", "Firefox", "Profiles", profileName),
            _ => null
        };
    }

    private async Task UpdateFirefoxProfilesIniAsync(
        string profileDir, string profileName, UserMapping mapping, CancellationToken ct)
    {
        var iniPath = Path.Combine(
            mapping.DestinationProfilePath, "AppData", "Roaming", "Mozilla", "Firefox", "profiles.ini");

        try
        {
            // Create a minimal profiles.ini if it doesn't exist
            if (!File.Exists(iniPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(iniPath)!);
                var content = $"""
                    [General]
                    StartWithLastProfile=1

                    [Profile0]
                    Name={profileName}
                    IsRelative=1
                    Path=Profiles/{profileName}
                    Default=1
                    """;
                await File.WriteAllTextAsync(iniPath, content, ct);
                _logger.LogDebug("Created Firefox profiles.ini for {User}", mapping.DestinationUsername);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Firefox profiles.ini");
        }
    }

    private static string[] GetExcludePatternsForBrowser(string browserName)
    {
        return browserName.ToLowerInvariant() switch
        {
            "chrome" or "google chrome" or "edge" or "microsoft edge" => ChromeExcludePatterns,
            "firefox" or "mozilla firefox" => FirefoxExcludePatterns,
            _ => []
        };
    }

    private static bool IsChromiumBased(string browserName)
    {
        return browserName.ToLowerInvariant() is "chrome" or "google chrome" or "edge" or "microsoft edge";
    }
}

/// <summary>
/// Manifest tracking captured browser data.
/// </summary>
public class BrowserCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<CapturedBrowserEntry> Browsers { get; set; } = [];
}

/// <summary>
/// A captured browser profile entry.
/// </summary>
public class CapturedBrowserEntry
{
    public string BrowserName { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string SourceUsername { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string CaptureSubDir { get; set; } = string.Empty;
}
