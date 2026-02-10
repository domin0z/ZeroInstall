using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Infrastructure;

/// <summary>
/// Builds the DI host for the backup agent.
/// </summary>
internal static class BackupHost
{
    public static IHost BuildHost(BackupConfiguration config, string configPath, bool serviceMode)
    {
        var basePath = AppContext.BaseDirectory;
        var logsPath = Path.Combine(basePath, "logs");
        Directory.CreateDirectory(logsPath);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsPath, "zim-backup-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (!serviceMode)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
        }

        var serilogLogger = loggerConfig.CreateLogger();
        Log.Logger = serilogLogger;

        var builder = Host.CreateDefaultBuilder();

        if (serviceMode)
        {
            builder.UseWindowsService(svc =>
            {
                svc.ServiceName = "ZeroInstallBackup";
            });
        }

        var host = builder
            .ConfigureServices(services =>
            {
                services.AddSingleton(config);
                services.AddSingleton<ISftpClientFactory, DefaultSftpClientFactory>();
                services.AddSingleton<IFileIndexService, FileIndexService>();
                services.AddSingleton<IRetentionService, RetentionService>();
                services.AddSingleton<IBackupExecutor, BackupExecutor>();
                services.AddSingleton<IConfigSyncService, ConfigSyncService>();
                services.AddSingleton<IStatusReporter, StatusReporter>();

                services.AddSingleton<BackupSchedulerService>(sp =>
                    new BackupSchedulerService(
                        sp.GetRequiredService<IBackupExecutor>(),
                        sp.GetRequiredService<IConfigSyncService>(),
                        sp.GetRequiredService<IStatusReporter>(),
                        config,
                        configPath,
                        sp.GetRequiredService<ILogger<BackupSchedulerService>>()));

                services.AddSingleton<IBackupScheduler>(sp => sp.GetRequiredService<BackupSchedulerService>());
                services.AddHostedService(sp => sp.GetRequiredService<BackupSchedulerService>());
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
