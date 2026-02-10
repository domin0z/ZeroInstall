using System.Drawing;
using System.Windows.Forms;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tray;

/// <summary>
/// System tray icon for the backup agent with context menu.
/// </summary>
internal class BackupTrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IBackupScheduler _scheduler;
    private readonly BackupConfiguration _config;
    private readonly ContextMenuStrip _contextMenu;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _backupNowItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _restoreItem;
    private readonly ToolStripMenuItem _exitItem;

    public event Func<Task>? BackupNowRequested;
    public event Action? SettingsRequested;
    public event Action? StatusRequested;
    public event Action? RestoreRequested;
    public event Action? ExitRequested;

    public BackupTrayIcon(IBackupScheduler scheduler, BackupConfiguration config)
    {
        _scheduler = scheduler;
        _config = config;

        _statusItem = new ToolStripMenuItem("Backup Status...");
        _statusItem.Click += (_, _) => StatusRequested?.Invoke();

        _backupNowItem = new ToolStripMenuItem("Back Up Now");
        _backupNowItem.Click += async (_, _) =>
        {
            if (BackupNowRequested != null)
                await BackupNowRequested.Invoke();
        };

        _settingsItem = new ToolStripMenuItem("Settings...");
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        _restoreItem = new ToolStripMenuItem("Request Restore...");
        _restoreItem.Click += (_, _) => RestoreRequested?.Invoke();

        _exitItem = new ToolStripMenuItem("Exit");
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_backupNowItem);
        _contextMenu.Items.Add(_settingsItem);
        _contextMenu.Items.Add(_restoreItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = $"ZeroInstall Backup - {_config.DisplayName}",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Use a default system icon
        _notifyIcon.Icon = SystemIcons.Shield;

        _notifyIcon.DoubleClick += (_, _) => StatusRequested?.Invoke();

        _scheduler.StateChanged += OnStateChanged;
        _scheduler.BackupCompleted += OnBackupCompleted;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);
    }

    internal void OnStateChanged(BackupSchedulerState state)
    {
        var statusText = state switch
        {
            BackupSchedulerState.Running => "Backup in progress...",
            BackupSchedulerState.Waiting => _scheduler.NextScheduledUtc.HasValue
                ? $"Next backup: {_scheduler.NextScheduledUtc.Value.ToLocalTime():g}"
                : "Waiting...",
            _ => "Idle"
        };

        _notifyIcon.Text = $"ZeroInstall Backup - {statusText}";
        _backupNowItem.Enabled = state != BackupSchedulerState.Running;
    }

    internal void OnBackupCompleted(BackupRunResult result)
    {
        var icon = result.ResultType switch
        {
            Enums.BackupRunResultType.Success => ToolTipIcon.Info,
            Enums.BackupRunResultType.PartialSuccess => ToolTipIcon.Warning,
            Enums.BackupRunResultType.Skipped => ToolTipIcon.Info,
            _ => ToolTipIcon.Error
        };

        var message = result.ResultType switch
        {
            Enums.BackupRunResultType.Success => $"Backed up {result.FilesUploaded} files successfully.",
            Enums.BackupRunResultType.PartialSuccess => $"Backed up {result.FilesUploaded} files, {result.FilesFailed} failed.",
            Enums.BackupRunResultType.Skipped => "No changes detected.",
            Enums.BackupRunResultType.QuotaExceeded => "Backup skipped: storage quota exceeded.",
            _ => $"Backup failed: {string.Join("; ", result.Errors.Take(2))}"
        };

        ShowBalloon("ZeroInstall Backup", message, icon);
    }

    public void Dispose()
    {
        _scheduler.StateChanged -= OnStateChanged;
        _scheduler.BackupCompleted -= OnBackupCompleted;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
