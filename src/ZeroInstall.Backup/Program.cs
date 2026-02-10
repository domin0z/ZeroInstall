using System.CommandLine;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Infrastructure;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;
using ZeroInstall.Backup.Tray;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Backup;

/// <summary>
/// Entry point for the ZeroInstall Backup Agent (zim-backup).
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        var configOption = new Option<string>("--config")
        {
            Description = "Path to backup-config.json",
            DefaultValueFactory = _ => Path.Combine(AppContext.BaseDirectory, "config", "backup-config.json")
        };

        var serviceOption = new Option<bool>("--service")
        {
            Description = "Run as a Windows Service (no tray icon)"
        };

        var backupNowOption = new Option<bool>("--backup-now")
        {
            Description = "Run a single backup immediately and exit"
        };

        // Root command -- tray mode (default)
        var rootCommand = new RootCommand("ZeroInstall Backup Agent (zim-backup) - Persistent scheduled backup to NAS")
        {
            configOption,
            serviceOption,
            backupNowOption
        };

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var configPath = parseResult.GetValue(configOption)!;
            var serviceMode = parseResult.GetValue(serviceOption);
            var backupNow = parseResult.GetValue(backupNowOption);

            var config = await LoadOrCreateConfigAsync(configPath, ct);

            if (backupNow)
            {
                return await RunSingleBackup(config, configPath, ct);
            }

            if (serviceMode)
            {
                return await RunAsService(config, configPath, ct);
            }

            return await RunAsTray(config, configPath, ct);
        });

        // install subcommand
        var installConfigOption = new Option<string>("--config")
        {
            Description = "Path to backup-config.json",
            Arity = ArgumentArity.ExactlyOne
        };

        var installCommand = new Command("install", "Install the backup agent as a Windows Service (requires admin)")
        {
            installConfigOption
        };

        installCommand.SetAction(async (parseResult, ct) =>
        {
            var cfgPath = parseResult.GetValue(installConfigOption)!;
            var exePath = Environment.ProcessPath ?? "zim-backup.exe";
            var processRunner = new WindowsProcessRunner();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupServiceInstaller>.Instance;
            var installer = new BackupServiceInstaller(processRunner, logger);

            var success = await installer.InstallAsync(exePath, cfgPath, ct);
            if (success)
            {
                Console.WriteLine("Backup service installed successfully.");
                Console.WriteLine("Start it with: sc.exe start ZeroInstallBackup");
            }
            else
            {
                Console.Error.WriteLine("Failed to install service. Are you running as administrator?");
            }
            return success ? 0 : 1;
        });

        // uninstall subcommand
        var uninstallCommand = new Command("uninstall", "Uninstall the backup agent Windows Service (requires admin)");
        uninstallCommand.SetAction(async (parseResult, ct) =>
        {
            var processRunner = new WindowsProcessRunner();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupServiceInstaller>.Instance;
            var installer = new BackupServiceInstaller(processRunner, logger);

            var success = await installer.UninstallAsync(ct);
            Console.WriteLine(success ? "Backup service uninstalled successfully." : "Failed to uninstall service.");
            return success ? 0 : 1;
        });

        // status subcommand
        var statusConfigOption = new Option<string>("--config")
        {
            Description = "Path to backup-config.json",
            DefaultValueFactory = _ => Path.Combine(AppContext.BaseDirectory, "config", "backup-config.json")
        };

        var statusCommand = new Command("status", "Show current backup status")
        {
            statusConfigOption
        };

        statusCommand.SetAction(async (parseResult, ct) =>
        {
            var cfgPath = parseResult.GetValue(statusConfigOption)!;
            var config = await LoadOrCreateConfigAsync(cfgPath, ct);

            Console.WriteLine($"Customer:    {config.DisplayName} ({config.CustomerId})");
            Console.WriteLine($"NAS Host:    {config.NasConnection.Host}");
            Console.WriteLine($"NAS Path:    {config.NasConnection.RemoteBasePath}");
            Console.WriteLine($"Schedule:    {config.FileBackupCron}");
            Console.WriteLine($"Encryption:  {(!string.IsNullOrEmpty(config.EncryptionPassphrase) ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Compression: {(config.CompressBeforeUpload ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Paths:       {string.Join(", ", config.BackupPaths)}");

            if (config.QuotaBytes > 0)
                Console.WriteLine($"Quota:       {config.QuotaBytes / (1024 * 1024)} MB");
            else
                Console.WriteLine("Quota:       Unlimited");

            return 0;
        });

        // backup subcommand (run once)
        var backupConfigOption = new Option<string>("--config")
        {
            Description = "Path to backup-config.json",
            DefaultValueFactory = _ => Path.Combine(AppContext.BaseDirectory, "config", "backup-config.json")
        };

        var backupCommand = new Command("backup", "Run a single file backup and exit")
        {
            backupConfigOption
        };

        backupCommand.SetAction(async (parseResult, ct) =>
        {
            var cfgPath = parseResult.GetValue(backupConfigOption)!;
            var config = await LoadOrCreateConfigAsync(cfgPath, ct);
            return await RunSingleBackup(config, cfgPath, ct);
        });

        rootCommand.Add(installCommand);
        rootCommand.Add(uninstallCommand);
        rootCommand.Add(statusCommand);
        rootCommand.Add(backupCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunSingleBackup(BackupConfiguration config, string configPath, CancellationToken ct)
    {
        Console.WriteLine($"Running backup for {config.DisplayName} ({config.CustomerId})...");

        using var host = BackupHost.BuildHost(config, configPath, serviceMode: false);
        var executor = host.Services.GetRequiredService<IBackupExecutor>();

        var progress = new Progress<string>(msg => Console.WriteLine($"  {msg}"));
        var result = await executor.RunFileBackupAsync(config, progress, ct);

        Console.WriteLine();
        Console.WriteLine($"Result:     {result.ResultType}");
        Console.WriteLine($"Scanned:    {result.FilesScanned} files");
        Console.WriteLine($"Uploaded:   {result.FilesUploaded} files");
        Console.WriteLine($"Failed:     {result.FilesFailed} files");
        Console.WriteLine($"Duration:   {result.Duration}");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine("Errors:");
            foreach (var error in result.Errors)
                Console.Error.WriteLine($"  - {error}");
        }

        return result.ResultType is BackupRunResultType.Success or BackupRunResultType.Skipped ? 0 : 1;
    }

    private static async Task<int> RunAsService(BackupConfiguration config, string configPath, CancellationToken ct)
    {
        using var host = BackupHost.BuildHost(config, configPath, serviceMode: true);
        await host.RunAsync(ct);
        return 0;
    }

    private static Task<int> RunAsTray(BackupConfiguration config, string configPath, CancellationToken ct)
    {
        var host = BackupHost.BuildHost(config, configPath, serviceMode: false);
        host.StartAsync(ct).GetAwaiter().GetResult();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BackupTrayApplication(host, configPath));

        return Task.FromResult(0);
    }

    internal static async Task<BackupConfiguration> LoadOrCreateConfigAsync(string configPath, CancellationToken ct = default)
    {
        if (File.Exists(configPath))
        {
            await using var stream = File.OpenRead(configPath);
            var config = await JsonSerializer.DeserializeAsync<BackupConfiguration>(stream, cancellationToken: ct);
            if (config != null)
                return config;
        }

        // Create a default config
        var defaultConfig = new BackupConfiguration
        {
            CustomerId = Environment.MachineName.ToLowerInvariant(),
            DisplayName = Environment.MachineName,
            BackupPaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop")
            },
            ExcludePatterns = { "*.tmp", "*.log", "Thumbs.db", "desktop.ini" }
        };

        // Save the default
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var writeStream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(writeStream, defaultConfig, JsonOptions, ct);

        return defaultConfig;
    }
}
