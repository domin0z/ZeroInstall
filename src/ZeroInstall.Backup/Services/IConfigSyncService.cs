using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Polls the NAS for config updates and syncs them locally.
/// </summary>
internal interface IConfigSyncService
{
    /// <summary>
    /// Checks the NAS for a newer config and returns the merged config.
    /// Returns the local config unchanged if NAS config is older or missing.
    /// </summary>
    Task<BackupConfiguration> SyncConfigAsync(
        BackupConfiguration localConfig,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a config from a local JSON file path.
    /// </summary>
    Task<BackupConfiguration?> LoadLocalConfigAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Saves a config to a local JSON file path.
    /// </summary>
    Task SaveLocalConfigAsync(BackupConfiguration config, string path, CancellationToken ct = default);
}
