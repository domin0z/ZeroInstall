using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ZeroInstall.Core.DependencyInjection;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Services;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE.Infrastructure;

/// <summary>
/// Builds the DI host for the WinPE restore environment.
/// </summary>
internal static class WinPeHost
{
    public static IHost BuildHost(bool verbose = false)
    {
        var basePath = AppContext.BaseDirectory;
        var logsPath = Path.Combine(basePath, "logs");
        Directory.CreateDirectory(logsPath);

        var minLevel = verbose
            ? Serilog.Events.LogEventLevel.Debug
            : Serilog.Events.LogEventLevel.Information;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.File(
                Path.Combine(logsPath, "zim-winpe-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(
                outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);

        var serilogLogger = loggerConfig.CreateLogger();
        Log.Logger = serilogLogger;

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddZeroInstallCore();
                services.AddTransient<ImageBrowserService>();
                services.AddTransient<RestoreOrchestrator>();
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
