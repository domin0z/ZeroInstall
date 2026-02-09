using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.DependencyInjection;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Infrastructure;

/// <summary>
/// Builds the DI host for the WPF application.
/// </summary>
internal static class AppHost
{
    /// <summary>
    /// Builds a configured <see cref="IHost"/> with all services registered.
    /// </summary>
    public static IHost BuildHost()
    {
        var basePath = GetBasePath();
        var logsPath = Path.Combine(basePath, "logs");
        Directory.CreateDirectory(logsPath);

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logsPath, "zim-app-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

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

                // Session state (singleton — shared across views)
                services.AddSingleton<ISessionState, SessionState>();

                // Migration coordinator
                services.AddTransient<IMigrationCoordinator, MigrationCoordinator>();

                // Navigation
                services.AddSingleton<INavigationService, NavigationService>();

                // ViewModels (transient — new instance per navigation)
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<WelcomeViewModel>();
                services.AddTransient<DiscoveryViewModel>();
                services.AddTransient<CaptureConfigViewModel>();
                services.AddTransient<RestoreConfigViewModel>();
                services.AddTransient<MigrationProgressViewModel>();
                services.AddTransient<JobSummaryViewModel>();

                // Window
                services.AddSingleton<MainWindow>();
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
