using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tray;

/// <summary>
/// Application context for the system tray backup agent.
/// Manages the tray icon and form lifecycle.
/// </summary>
internal class BackupTrayApplication : ApplicationContext
{
    private readonly IHost _host;
    private readonly BackupTrayIcon _trayIcon;
    private readonly BackupConfiguration _config;
    private readonly IBackupScheduler _scheduler;
    private readonly IStatusReporter _statusReporter;
    private readonly IConfigSyncService _configSync;
    private readonly string _configPath;

    public BackupTrayApplication(IHost host, string configPath)
    {
        _host = host;
        _configPath = configPath;
        _config = host.Services.GetRequiredService<BackupConfiguration>();
        _scheduler = host.Services.GetRequiredService<IBackupScheduler>();
        _statusReporter = host.Services.GetRequiredService<IStatusReporter>();
        _configSync = host.Services.GetRequiredService<IConfigSyncService>();

        _trayIcon = new BackupTrayIcon(_scheduler, _config);
        _trayIcon.BackupNowRequested += OnBackupNowRequested;
        _trayIcon.StatusRequested += OnStatusRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.RestoreRequested += OnRestoreRequested;
        _trayIcon.ExitRequested += OnExitRequested;
    }

    private async Task OnBackupNowRequested()
    {
        await _scheduler.TriggerBackupNowAsync();
    }

    private void OnStatusRequested()
    {
        using var form = new BackupStatusForm(_scheduler, _config);
        form.ShowDialog();
    }

    private void OnSettingsRequested()
    {
        using var form = new BackupSettingsForm(_config);
        if (form.ShowDialog() == DialogResult.OK && form.Saved)
        {
            Task.Run(async () =>
            {
                await _configSync.SaveLocalConfigAsync(_config, _configPath);
            });
        }
    }

    private void OnRestoreRequested()
    {
        var result = MessageBox.Show(
            "This will send a restore request to your technician.\n\nDo you want to proceed?",
            "Request Restore",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            var request = new RestoreRequest
            {
                CustomerId = _config.CustomerId,
                MachineName = Environment.MachineName,
                Scope = RestoreScope.Full,
                RequestedUtc = DateTime.UtcNow
            };

            Task.Run(async () =>
            {
                await _statusReporter.SubmitRestoreRequestAsync(_config, request);
            });

            _trayIcon.ShowBalloon("Restore Request", "Your restore request has been sent to the technician.");
        }
    }

    private void OnExitRequested()
    {
        _trayIcon.Dispose();
        _host.StopAsync().GetAwaiter().GetResult();
        ExitThread();
    }
}
