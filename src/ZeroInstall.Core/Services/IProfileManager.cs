using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Manages migration profile templates (load, save, list) from local storage and NAS.
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// Lists all available profiles from local storage (flash drive).
    /// </summary>
    Task<IReadOnlyList<MigrationProfile>> ListLocalProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists all available profiles from the configured NAS path.
    /// </summary>
    Task<IReadOnlyList<MigrationProfile>> ListNasProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads a profile by name from local storage.
    /// </summary>
    Task<MigrationProfile?> LoadLocalProfileAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Loads a profile by name from the NAS.
    /// </summary>
    Task<MigrationProfile?> LoadNasProfileAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Saves a profile to local storage (flash drive).
    /// </summary>
    Task SaveLocalProfileAsync(MigrationProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Deletes a profile from local storage.
    /// </summary>
    Task DeleteLocalProfileAsync(string name, CancellationToken ct = default);
}
