using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Reports backup status and restore requests to the NAS via SFTP.
/// </summary>
internal class StatusReporter : IStatusReporter
{
    private readonly ISftpClientFactory _sftpClientFactory;
    private readonly ILogger<StatusReporter> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StatusReporter(ISftpClientFactory sftpClientFactory, ILogger<StatusReporter> logger)
    {
        _sftpClientFactory = sftpClientFactory;
        _logger = logger;
    }

    public async Task ReportStatusAsync(BackupConfiguration config, BackupStatus status, CancellationToken ct = default)
    {
        try
        {
            using var client = _sftpClientFactory.Create(config.NasConnection);
            client.Connect();

            var statusPath = $"{config.GetNasStatusPath()}/backup-status.json";
            EnsureRemoteDirectory(client, config.GetNasStatusPath());

            var json = JsonSerializer.Serialize(status, JsonOptions);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            client.UploadFile(stream, statusPath);

            _logger.LogDebug("Status report uploaded to {Path}", statusPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload status report");
        }

        await Task.CompletedTask;
    }

    public async Task SubmitRestoreRequestAsync(BackupConfiguration config, RestoreRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = _sftpClientFactory.Create(config.NasConnection);
            client.Connect();

            var requestPath = $"{config.GetNasStatusPath()}/restore-request.json";
            EnsureRemoteDirectory(client, config.GetNasStatusPath());

            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            client.UploadFile(stream, requestPath);

            _logger.LogInformation("Restore request submitted to {Path}", requestPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to submit restore request");
        }

        await Task.CompletedTask;
    }

    private static void EnsureRemoteDirectory(ISftpClientWrapper client, string path)
    {
        if (client.Exists(path))
            return;

        var parts = path.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();
        var current = "";

        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }
}
