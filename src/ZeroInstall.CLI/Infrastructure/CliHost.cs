using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ZeroInstall.Core.DependencyInjection;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Infrastructure;

/// <summary>
/// Builds the DI host for the CLI application.
/// </summary>
internal static class CliHost
{
    /// <summary>
    /// Builds a configured <see cref="IHost"/> with all services registered.
    /// </summary>
    public static IHost BuildHost(bool verbose)
    {
        var basePath = GetBasePath();
        var logsPath = Path.Combine(basePath, "logs");
        Directory.CreateDirectory(logsPath);

        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(logsPath, "zim-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (verbose)
        {
            loggerConfig.MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
        }
        else
        {
            loggerConfig.MinimumLevel.Information();
        }

        var serilogLogger = loggerConfig.CreateLogger();
        Log.Logger = serilogLogger;

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddZeroInstallCore();

                // Job logger — uses {basePath}/data for jobs and reports
                var dataPath = Path.Combine(basePath, "data");
                services.AddSingleton<IJobLogger>(sp =>
                    new JsonJobLogger(dataPath, sp.GetRequiredService<ILogger<JsonJobLogger>>()));

                // Profile manager — local profiles in {basePath}/profiles, optional NAS
                var profilesPath = Path.Combine(basePath, "profiles");
                services.AddSingleton<IProfileManager>(sp =>
                    new JsonProfileManager(profilesPath, null, sp.GetRequiredService<ILogger<JsonProfileManager>>()));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new SerilogLoggerProvider(serilogLogger, dispose: false));
            })
            .Build();

        return host;
    }

    /// <summary>
    /// Gets the base path for the portable application (directory containing the executable).
    /// </summary>
    public static string GetBasePath() => AppContext.BaseDirectory;
}
