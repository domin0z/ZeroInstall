using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZeroInstall.Dashboard.Services;

internal class NasScannerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardConfiguration _config;
    private readonly ILogger<NasScannerBackgroundService> _logger;

    public NasScannerBackgroundService(
        IServiceScopeFactory scopeFactory,
        DashboardConfiguration config,
        ILogger<NasScannerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_config.NasSftpHost))
        {
            _logger.LogInformation("NAS scanner background service disabled: no SFTP host configured");
            return;
        }

        _logger.LogInformation("NAS scanner background service started, interval={IntervalMin}m",
            _config.NasScanIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<INasScannerService>();
                await scanner.ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NAS scanner iteration failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_config.NasScanIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("NAS scanner background service stopped");
    }
}
