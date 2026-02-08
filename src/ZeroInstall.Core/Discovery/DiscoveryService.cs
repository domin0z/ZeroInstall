using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Top-level discovery service that orchestrates application, user profile,
/// and system settings discovery, then aggregates results into a selectable checklist.
/// </summary>
public class DiscoveryService : IDiscoveryService
{
    private readonly ApplicationDiscoveryService _appDiscovery;
    private readonly UserProfileDiscoveryService _profileDiscovery;
    private readonly SystemSettingsDiscoveryService _settingsDiscovery;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(
        ApplicationDiscoveryService appDiscovery,
        UserProfileDiscoveryService profileDiscovery,
        SystemSettingsDiscoveryService settingsDiscovery,
        ILogger<DiscoveryService> logger)
    {
        _appDiscovery = appDiscovery;
        _profileDiscovery = profileDiscovery;
        _settingsDiscovery = settingsDiscovery;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredApplication>> DiscoverApplicationsAsync(CancellationToken ct = default)
    {
        return await _appDiscovery.DiscoverAsync(ct);
    }

    public async Task<IReadOnlyList<UserProfile>> DiscoverUserProfilesAsync(CancellationToken ct = default)
    {
        return await _profileDiscovery.DiscoverAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSetting>> DiscoverSystemSettingsAsync(CancellationToken ct = default)
    {
        return await _settingsDiscovery.DiscoverAsync(ct);
    }

    public async Task<IReadOnlyList<MigrationItem>> DiscoverAllAsync(
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var items = new List<MigrationItem>();

        // Discover applications
        statusProgress?.Report("Scanning installed applications...");
        var apps = await _appDiscovery.DiscoverAsync(ct);
        foreach (var app in apps)
        {
            items.Add(new MigrationItem
            {
                DisplayName = app.Name,
                Description = $"{app.Publisher} {app.Version}".Trim(),
                ItemType = MigrationItemType.Application,
                RecommendedTier = app.RecommendedTier,
                EstimatedSizeBytes = app.EstimatedSizeBytes,
                SourceData = app
            });
        }

        // Discover user profiles
        statusProgress?.Report("Scanning user profiles...");
        var profiles = await _profileDiscovery.DiscoverAsync(ct);
        foreach (var profile in profiles)
        {
            items.Add(new MigrationItem
            {
                DisplayName = $"User Profile: {profile.Username}",
                Description = FormatProfileDescription(profile),
                ItemType = MigrationItemType.UserProfile,
                RecommendedTier = MigrationTier.RegistryFile,
                EstimatedSizeBytes = profile.EstimatedSizeBytes,
                SourceData = profile
            });

            // Add browser profiles as separate selectable items
            foreach (var browser in profile.BrowserProfiles)
            {
                items.Add(new MigrationItem
                {
                    DisplayName = $"{browser.BrowserName} ({browser.ProfileName}) — {profile.Username}",
                    Description = $"Browser profile for {profile.Username}",
                    ItemType = MigrationItemType.BrowserData,
                    RecommendedTier = MigrationTier.RegistryFile,
                    EstimatedSizeBytes = browser.EstimatedSizeBytes,
                    SourceData = browser
                });
            }
        }

        // Discover system settings
        statusProgress?.Report("Scanning system settings...");
        var settings = await _settingsDiscovery.DiscoverAsync(ct);
        foreach (var setting in settings)
        {
            items.Add(new MigrationItem
            {
                DisplayName = setting.Name,
                Description = setting.Description,
                ItemType = MigrationItemType.SystemSetting,
                RecommendedTier = MigrationTier.RegistryFile,
                EstimatedSizeBytes = 0, // Settings are tiny
                SourceData = setting
            });
        }

        statusProgress?.Report($"Discovery complete: {items.Count} items found.");
        _logger.LogInformation(
            "Discovery complete: {Apps} apps, {Profiles} profiles, {Settings} settings, {Total} total items",
            apps.Count, profiles.Count, settings.Count, items.Count);

        return items;
    }

    private static string FormatProfileDescription(UserProfile profile)
    {
        var parts = new List<string>();

        if (profile.Folders.Documents is not null) parts.Add("Documents");
        if (profile.Folders.Desktop is not null) parts.Add("Desktop");
        if (profile.Folders.Downloads is not null) parts.Add("Downloads");
        if (profile.BrowserProfiles.Count > 0)
            parts.Add($"{profile.BrowserProfiles.Count} browser profile(s)");
        if (profile.EmailData.Count > 0)
            parts.Add($"{profile.EmailData.Count} email client(s)");

        var sizeStr = FormatBytes(profile.EstimatedSizeBytes);
        return $"{string.Join(", ", parts)} — {sizeStr}";
    }

    internal static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
