using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroInstall.Agent.Infrastructure;
using ZeroInstall.Agent.Models;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Hosted service for portable (console) mode. Runs the transfer once, then stops the application.
/// </summary>
internal class AgentPortableService : IHostedService
{
    private readonly IAgentTransferService _transferService;
    private readonly AgentOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AgentPortableService> _logger;
    private Task? _transferTask;

    public AgentPortableService(
        IAgentTransferService transferService,
        AgentOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<AgentPortableService> logger)
    {
        _transferService = transferService;
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        AgentConsoleUI.WriteHeader(_options);

        _transferService.StatusChanged += status => AgentConsoleUI.WriteStatus(status);
        _transferService.ProgressChanged += progress => AgentConsoleUI.WriteProgress(progress);

        var stopwatch = Stopwatch.StartNew();

        _transferTask = Task.Run(async () =>
        {
            try
            {
                if (_options.Role == AgentRole.Source)
                    await _transferService.RunAsSourceAsync(cancellationToken);
                else
                    await _transferService.RunAsDestinationAsync(cancellationToken);

                stopwatch.Stop();

                // Count files in the directory for summary
                var dirPath = _options.DirectoryPath;
                if (Directory.Exists(dirPath))
                {
                    var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                    var totalBytes = files.Sum(f => new FileInfo(f).Length);
                    AgentConsoleUI.WriteSummary(files.Length, totalBytes, stopwatch.Elapsed);
                }
            }
            catch (OperationCanceledException)
            {
                AgentConsoleUI.WriteStatus("Transfer cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer failed");
                AgentConsoleUI.WriteStatus($"Error: {ex.Message}");
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_transferTask is not null)
        {
            try
            {
                await _transferTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }
}
