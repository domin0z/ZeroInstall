using System.Drawing;
using System.Windows.Forms;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tray;

/// <summary>
/// WinForms dialog showing backup status, NAS usage, and recent history.
/// </summary>
internal class BackupStatusForm : Form
{
    private readonly IBackupScheduler _scheduler;
    private readonly BackupConfiguration _config;
    private readonly Label _statusLabel;
    private readonly Label _nextBackupLabel;
    private readonly Label _customerLabel;
    private readonly ProgressBar _quotaBar;
    private readonly Label _quotaLabel;
    private readonly Button _closeButton;

    public BackupStatusForm(IBackupScheduler scheduler, BackupConfiguration config)
    {
        _scheduler = scheduler;
        _config = config;

        Text = "ZeroInstall Backup - Status";
        Size = new Size(420, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _customerLabel = new Label
        {
            Text = $"Customer: {_config.DisplayName} ({_config.CustomerId})",
            Location = new Point(15, 15),
            Size = new Size(380, 20),
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Text = $"Status: {_scheduler.State}",
            Location = new Point(15, 50),
            Size = new Size(380, 20)
        };

        _nextBackupLabel = new Label
        {
            Text = _scheduler.NextScheduledUtc.HasValue
                ? $"Next backup: {_scheduler.NextScheduledUtc.Value.ToLocalTime():g}"
                : "Next backup: Not scheduled",
            Location = new Point(15, 80),
            Size = new Size(380, 20)
        };

        var quotaGroupLabel = new Label
        {
            Text = "NAS Storage:",
            Location = new Point(15, 120),
            Size = new Size(380, 20)
        };

        _quotaBar = new ProgressBar
        {
            Location = new Point(15, 145),
            Size = new Size(370, 25),
            Minimum = 0,
            Maximum = 100,
            Value = _config.QuotaBytes > 0 ? 0 : 50 // Placeholder
        };

        _quotaLabel = new Label
        {
            Text = _config.QuotaBytes > 0
                ? $"Quota: {FormatBytes(_config.QuotaBytes)}"
                : "Quota: Unlimited",
            Location = new Point(15, 175),
            Size = new Size(380, 20)
        };

        var scheduleLabel = new Label
        {
            Text = $"File backup schedule: {_config.FileBackupCron}",
            Location = new Point(15, 210),
            Size = new Size(380, 20)
        };

        var encryptionLabel = new Label
        {
            Text = $"Encryption: {(!string.IsNullOrEmpty(_config.EncryptionPassphrase) ? "Enabled" : "Disabled")}",
            Location = new Point(15, 235),
            Size = new Size(380, 20)
        };

        _closeButton = new Button
        {
            Text = "Close",
            Location = new Point(305, 260),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[]
        {
            _customerLabel, _statusLabel, _nextBackupLabel,
            quotaGroupLabel, _quotaBar, _quotaLabel,
            scheduleLabel, encryptionLabel, _closeButton
        });

        AcceptButton = _closeButton;
    }

    internal static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824L)
            return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576L)
            return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
