using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Top-level orchestrator for Phase 7: Profile & Settings migration.
/// Coordinates user account creation, profile transfer, browser data,
/// path remapping, and system settings replay.
/// </summary>
public class ProfileSettingsMigratorService
{
    private const string ManifestFileName = "profile-settings-manifest.json";
    private const string ProfilesSubDir = "profiles";
    private const string BrowserDataSubDir = "browser-data";
    private const string SystemSettingsSubDir = "system-settings";

    private readonly IUserAccountManager _accountManager;
    private readonly ProfileTransferService _profileTransfer;
    private readonly IUserPathRemapper _pathRemapper;
    private readonly BrowserDataService _browserData;
    private readonly SystemSettingsReplayService _systemSettings;
    private readonly ILogger<ProfileSettingsMigratorService> _logger;

    public ProfileSettingsMigratorService(
        IUserAccountManager accountManager,
        ProfileTransferService profileTransfer,
        IUserPathRemapper pathRemapper,
        BrowserDataService browserData,
        SystemSettingsReplayService systemSettings,
        ILogger<ProfileSettingsMigratorService> logger)
    {
        _accountManager = accountManager;
        _profileTransfer = profileTransfer;
        _pathRemapper = pathRemapper;
        _browserData = browserData;
        _systemSettings = systemSettings;
        _logger = logger;
    }

    /// <summary>
    /// Prepares user accounts on the destination machine.
    /// Creates accounts that don't exist and populates SID/profile path on each mapping.
    /// </summary>
    public async Task PrepareUserAccountsAsync(
        IList<UserMapping> userMappings,
        CancellationToken ct = default)
    {
        foreach (var mapping in userMappings)
        {
            ct.ThrowIfCancellationRequested();

            var exists = await _accountManager.UserExistsAsync(mapping.DestinationUsername, ct);

            if (!exists && mapping.CreateIfMissing)
            {
                _logger.LogInformation("Creating user account: {Username}", mapping.DestinationUsername);

                var password = mapping.NewAccountPassword ?? GenerateDefaultPassword();
                var sid = await _accountManager.CreateUserAsync(
                    mapping.DestinationUsername, password, isAdmin: false, ct);

                mapping.DestinationSid = sid;
            }
            else if (!exists)
            {
                _logger.LogWarning("User {Username} does not exist and CreateIfMissing is false",
                    mapping.DestinationUsername);
                continue;
            }

            // Populate SID if not already set
            if (string.IsNullOrEmpty(mapping.DestinationSid))
            {
                mapping.DestinationSid = await _accountManager.GetUserSidAsync(
                    mapping.DestinationUsername, ct);
            }

            // Populate profile path if not already set
            if (string.IsNullOrEmpty(mapping.DestinationProfilePath))
            {
                var profilePath = await _accountManager.GetUserProfilePathAsync(
                    mapping.DestinationUsername, ct);
                mapping.DestinationProfilePath = profilePath ?? $@"C:\Users\{mapping.DestinationUsername}";
            }

            _logger.LogDebug("Prepared mapping: {Source} → {Dest} (SID: {Sid}, Path: {Path})",
                mapping.SourceUser.Username, mapping.DestinationUsername,
                mapping.DestinationSid, mapping.DestinationProfilePath);
        }
    }

    /// <summary>
    /// Captures all profile and settings data from the source machine.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputPath);

        // Capture user profiles
        statusProgress?.Report("Capturing user profiles...");
        var profilesDir = Path.Combine(outputPath, ProfilesSubDir);
        await _profileTransfer.CaptureAsync(items, profilesDir, statusProgress, ct);

        // Capture browser data
        statusProgress?.Report("Capturing browser data...");
        var browserDir = Path.Combine(outputPath, BrowserDataSubDir);
        await _browserData.CaptureAsync(items, browserDir, statusProgress, ct);

        // Capture system settings
        statusProgress?.Report("Capturing system settings...");
        var settingsDir = Path.Combine(outputPath, SystemSettingsSubDir);
        await _systemSettings.CaptureAsync(items, settingsDir, statusProgress, ct);

        // Write top-level manifest
        var manifest = new ProfileSettingsManifest
        {
            CapturedUtc = DateTime.UtcNow,
            HasProfiles = Directory.Exists(profilesDir),
            HasBrowserData = Directory.Exists(browserDir),
            HasSystemSettings = Directory.Exists(settingsDir)
        };

        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Profile & settings capture complete at {Path}", outputPath);
    }

    /// <summary>
    /// Restores all profile and settings data to the destination machine.
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IList<UserMapping> userMappings,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        // Step 1: Prepare user accounts
        statusProgress?.Report("Preparing user accounts...");
        await PrepareUserAccountsAsync(userMappings, ct);

        // Cast once for sub-services that expect IReadOnlyList
        var readOnlyMappings = userMappings.ToList().AsReadOnly();

        // Step 2: Restore user profiles
        var profilesDir = Path.Combine(inputPath, ProfilesSubDir);
        if (Directory.Exists(profilesDir))
        {
            statusProgress?.Report("Restoring user profiles...");
            await _profileTransfer.RestoreAsync(profilesDir, readOnlyMappings, statusProgress, ct);
        }

        // Step 3: Restore browser data
        var browserDir = Path.Combine(inputPath, BrowserDataSubDir);
        if (Directory.Exists(browserDir))
        {
            statusProgress?.Report("Restoring browser data...");
            await _browserData.RestoreAsync(browserDir, readOnlyMappings, statusProgress, ct);
        }

        // Step 4: Remap user paths (per mapping)
        foreach (var mapping in userMappings)
        {
            if (!mapping.RequiresPathRemapping) continue;

            statusProgress?.Report($"Remapping paths for {mapping.DestinationUsername}...");
            await _pathRemapper.RemapPathsAsync(
                mapping, mapping.DestinationProfilePath, statusProgress, ct);
        }

        // Step 5: Restore system settings (after path remapping)
        var settingsDir = Path.Combine(inputPath, SystemSettingsSubDir);
        if (Directory.Exists(settingsDir))
        {
            statusProgress?.Report("Restoring system settings...");
            await _systemSettings.RestoreAsync(settingsDir, readOnlyMappings, statusProgress, ct);
        }

        _logger.LogInformation("Profile & settings restore complete from {Path}", inputPath);
    }

    private static string GenerateDefaultPassword()
    {
        return $"Temp{Guid.NewGuid().ToString("N")[..8]}!";
    }
}

/// <summary>
/// Top-level manifest for a profile & settings capture.
/// </summary>
public class ProfileSettingsManifest
{
    public DateTime CapturedUtc { get; set; }
    public bool HasProfiles { get; set; }
    public bool HasBrowserData { get; set; }
    public bool HasSystemSettings { get; set; }
}
