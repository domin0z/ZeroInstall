using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Scans the source machine to discover installed applications, user profiles, and system settings.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Discovers all installed applications on the machine.
    /// </summary>
    Task<IReadOnlyList<DiscoveredApplication>> DiscoverApplicationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Discovers all user profiles on the machine.
    /// </summary>
    Task<IReadOnlyList<UserProfile>> DiscoverUserProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Discovers all transferable system settings on the machine.
    /// </summary>
    Task<IReadOnlyList<SystemSetting>> DiscoverSystemSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs full discovery and aggregates results into a list of selectable MigrationItems.
    /// </summary>
    Task<IReadOnlyList<MigrationItem>> DiscoverAllAsync(
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);
}
