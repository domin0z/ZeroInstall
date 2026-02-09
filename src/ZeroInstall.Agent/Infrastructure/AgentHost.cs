using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Agent.Infrastructure;

/// <summary>
/// Builds the DI host for the transfer agent.
/// </summary>
internal static class AgentHost
{
    public static IHost BuildHost(AgentOptions options)
    {
        var basePath = AppContext.BaseDirectory;
        var logsPath = Path.Combine(basePath, "logs");
        Directory.CreateDirectory(logsPath);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsPath, "zim-agent-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (options.Mode == AgentMode.Portable)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
        }

        var serilogLogger = loggerConfig.CreateLogger();
        Log.Logger = serilogLogger;

        var builder = Host.CreateDefaultBuilder();

        if (options.Mode == AgentMode.Service)
        {
            builder.UseWindowsService(config =>
            {
                config.ServiceName = "ZeroInstallAgent";
            });
        }

        var host = builder
            .ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IAgentTransferService, AgentTransferService>();

                if (options.Mode == AgentMode.Portable)
                    services.AddHostedService<AgentPortableService>();
                else
                    services.AddHostedService<AgentWindowsService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new SerilogLoggerProvider(serilogLogger, dispose: false));
            })
            .Build();

        return host;
    }
}
