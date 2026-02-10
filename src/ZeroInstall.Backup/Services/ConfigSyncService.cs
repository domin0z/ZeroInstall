using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Polls the NAS for config updates and syncs them locally.
/// Downloads backup-config.json from the NAS config path, compares timestamps, uses newer.
/// </summary>
internal class ConfigSyncService : IConfigSyncService
{
    private readonly ISftpClientFactory _sftpClientFactory;
    private readonly ILogger<ConfigSyncService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ConfigSyncService(ISftpClientFactory sftpClientFactory, ILogger<ConfigSyncService> logger)
    {
        _sftpClientFactory = sftpClientFactory;
        _logger = logger;
    }

    public async Task<BackupConfiguration> SyncConfigAsync(
        BackupConfiguration localConfig,
        CancellationToken ct = default)
    {
        try
        {
            using var client = _sftpClientFactory.Create(localConfig.NasConnection);
            client.Connect();

            var remoteConfigPath = $"{localConfig.GetNasConfigPath()}/backup-config.json";

            if (!client.Exists(remoteConfigPath))
            {
                _logger.LogDebug("No remote config found at {Path}, using local", remoteConfigPath);
                return localConfig;
            }

            using var stream = client.OpenRead(remoteConfigPath);
            var remoteConfig = await JsonSerializer.DeserializeAsync<BackupConfiguration>(stream, cancellationToken: ct);

            if (remoteConfig == null)
            {
                _logger.LogWarning("Failed to deserialize remote config from {Path}", remoteConfigPath);
                return localConfig;
            }

            if (remoteConfig.LastModifiedUtc > localConfig.LastModifiedUtc)
            {
                _logger.LogInformation("Remote config is newer ({Remote} > {Local}), using remote",
                    remoteConfig.LastModifiedUtc, localConfig.LastModifiedUtc);

                // Preserve local-only settings that shouldn't be overridden from NAS
                remoteConfig.NasConnection = localConfig.NasConnection;
                return remoteConfig;
            }

            _logger.LogDebug("Local config is up to date");
            return localConfig;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Config sync failed, using local config");
            return localConfig;
        }
    }

    public async Task<BackupConfiguration?> LoadLocalConfigAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<BackupConfiguration>(stream, cancellationToken: ct);
    }

    public async Task SaveLocalConfigAsync(BackupConfiguration config, string path, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, ct);
    }
}
