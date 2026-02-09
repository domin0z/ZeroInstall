using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroInstall.Agent.Models;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Background service for Windows Service mode. Loops: listen → authenticate → transfer → repeat.
/// Only supports source role (waits for incoming connections).
/// </summary>
internal class AgentWindowsService : BackgroundService
{
    private readonly IAgentTransferService _transferService;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentWindowsService> _logger;

    public AgentWindowsService(
        IAgentTransferService transferService,
        AgentOptions options,
        ILogger<AgentWindowsService> logger)
    {
        _transferService = transferService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZeroInstall Agent service started (source mode, port {Port})", _options.Port);

        _transferService.StatusChanged += status =>
            _logger.LogInformation("Status: {Status}", status);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Waiting for incoming transfer connection...");
                await _transferService.RunAsSourceAsync(stoppingToken);
                _logger.LogInformation("Transfer completed successfully");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer failed, will retry in 5 seconds");
                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("ZeroInstall Agent service stopped");
    }
}
