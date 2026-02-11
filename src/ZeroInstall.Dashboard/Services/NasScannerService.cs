using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Transport;
using ZeroInstall.Dashboard.Hubs;

namespace ZeroInstall.Dashboard.Services;

internal class NasScannerService : INasScannerService
{
    private readonly DashboardConfiguration _config;
    private readonly IDashboardDataService _dataService;
    private readonly IAlertService _alertService;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<NasScannerService> _logger;

    public NasScannerService(
        DashboardConfiguration config,
        IDashboardDataService dataService,
        IAlertService alertService,
        IHubContext<DashboardHub> hubContext,
        ILogger<NasScannerService> logger)
    {
        _config = config;
        _dataService = dataService;
        _alertService = alertService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_config.NasSftpHost))
        {
            _logger.LogDebug("NAS scanner skipped: no SFTP host configured");
            return;
        }

        ISftpClientWrapper? client = null;
        try
        {
            client = CreateClient();
            client.Connect();

            var customersPath = $"{_config.NasSftpBasePath.TrimEnd('/')}/customers";

            if (!client.Exists(customersPath))
            {
                _logger.LogDebug("NAS customers directory does not exist: {Path}", customersPath);
                return;
            }

            var customerDirs = client.ListDirectory(customersPath)
                .Where(f => f.IsDirectory && f.Name != "." && f.Name != "..")
                .ToList();

            _logger.LogInformation("NAS scan found {Count} customer directories", customerDirs.Count);

            foreach (var dir in customerDirs)
            {
                ct.ThrowIfCancellationRequested();

                var statusPath = $"{dir.FullName}/status/backup-status.json";
                try
                {
                    if (!client.Exists(statusPath))
                        continue;

                    using var stream = client.OpenRead(statusPath);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var json = await reader.ReadToEndAsync(ct);

                    var record = await _dataService.UpsertBackupStatusAsync(
                        dir.Name, GetMachineNameFromJson(json), json, ct);

                    await _alertService.EvaluateBackupStatusAsync(record, ct);

                    await _hubContext.Clients.All.SendAsync("BackupStatusChanged", dir.Name, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read backup status for customer {Customer}", dir.Name);
                }
            }

            var stats = await _dataService.GetStatsAsync(ct);
            await _hubContext.Clients.All.SendAsync("StatsChanged", stats, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NAS scan failed");
        }
        finally
        {
            if (client is not null)
            {
                try { client.Disconnect(); } catch { }
                client.Dispose();
            }
        }
    }

    private ISftpClientWrapper CreateClient()
    {
        return new SftpClientWrapper(
            _config.NasSftpHost!,
            _config.NasSftpPort,
            _config.NasSftpUser ?? "anonymous",
            _config.NasSftpPassword,
            _config.NasSftpKeyPath);
    }

    private static string GetMachineNameFromJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("machineName", out var mn))
                return mn.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("MachineName", out var mn2))
                return mn2.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }
}
