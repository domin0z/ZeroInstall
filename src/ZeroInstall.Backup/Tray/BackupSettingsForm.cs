using System.Drawing;
using System.Windows.Forms;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tray;

/// <summary>
/// WinForms dialog for editing backup settings.
/// </summary>
internal class BackupSettingsForm : Form
{
    private readonly BackupConfiguration _config;
    private readonly TextBox _pathsBox;
    private readonly TextBox _excludeBox;
    private readonly TextBox _cronBox;
    private readonly CheckBox _encryptCheckBox;
    private readonly CheckBox _compressCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public BackupSettingsForm(BackupConfiguration config)
    {
        _config = config;

        Text = "ZeroInstall Backup - Settings";
        Size = new Size(480, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var pathsLabel = new Label
        {
            Text = "Backup paths (one per line):",
            Location = new Point(15, 15),
            Size = new Size(440, 20)
        };

        _pathsBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine, _config.BackupPaths),
            Location = new Point(15, 40),
            Size = new Size(430, 80)
        };

        var excludeLabel = new Label
        {
            Text = "Exclude patterns (one per line):",
            Location = new Point(15, 130),
            Size = new Size(440, 20)
        };

        _excludeBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine, _config.ExcludePatterns),
            Location = new Point(15, 155),
            Size = new Size(430, 60)
        };

        var cronLabel = new Label
        {
            Text = "Backup schedule (cron):",
            Location = new Point(15, 225),
            Size = new Size(200, 20)
        };

        _cronBox = new TextBox
        {
            Text = _config.FileBackupCron,
            Location = new Point(215, 222),
            Size = new Size(230, 25)
        };

        _encryptCheckBox = new CheckBox
        {
            Text = "Encrypt backups (AES-256)",
            Checked = !string.IsNullOrEmpty(_config.EncryptionPassphrase),
            Location = new Point(15, 260),
            Size = new Size(440, 25)
        };

        _compressCheckBox = new CheckBox
        {
            Text = "Compress before upload",
            Checked = _config.CompressBeforeUpload,
            Location = new Point(15, 290),
            Size = new Size(440, 25)
        };

        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(280, 340),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };
        _saveButton.Click += OnSave;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(370, 340),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            pathsLabel, _pathsBox,
            excludeLabel, _excludeBox,
            cronLabel, _cronBox,
            _encryptCheckBox, _compressCheckBox,
            _saveButton, _cancelButton
        });

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    /// <summary>
    /// Gets whether settings were modified and saved.
    /// </summary>
    public bool Saved { get; private set; }

    private void OnSave(object? sender, EventArgs e)
    {
        _config.BackupPaths = _pathsBox.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        _config.ExcludePatterns = _excludeBox.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        _config.FileBackupCron = _cronBox.Text.Trim();
        _config.CompressBeforeUpload = _compressCheckBox.Checked;
        _config.LastModifiedUtc = DateTime.UtcNow;

        Saved = true;
    }
}
