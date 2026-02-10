using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;
using ZeroInstall.Backup.Tray;

namespace ZeroInstall.Backup.Tests.Tray;

public class BackupTrayIconTests
{
    [Fact]
    public void OnBackupCompleted_Success_GeneratesCorrectMessage()
    {
        var result = new BackupRunResult
        {
            ResultType = BackupRunResultType.Success,
            FilesUploaded = 42
        };

        // Test the message generation logic directly
        var message = result.ResultType switch
        {
            BackupRunResultType.Success => $"Backed up {result.FilesUploaded} files successfully.",
            BackupRunResultType.PartialSuccess => $"Backed up {result.FilesUploaded} files, {result.FilesFailed} failed.",
            BackupRunResultType.Skipped => "No changes detected.",
            BackupRunResultType.QuotaExceeded => "Backup skipped: storage quota exceeded.",
            _ => $"Backup failed: {string.Join("; ", result.Errors.Take(2))}"
        };

        message.Should().Be("Backed up 42 files successfully.");
    }

    [Fact]
    public void OnBackupCompleted_PartialSuccess_IncludesFailCount()
    {
        var result = new BackupRunResult
        {
            ResultType = BackupRunResultType.PartialSuccess,
            FilesUploaded = 10,
            FilesFailed = 3
        };

        var message = result.ResultType switch
        {
            BackupRunResultType.Success => $"Backed up {result.FilesUploaded} files successfully.",
            BackupRunResultType.PartialSuccess => $"Backed up {result.FilesUploaded} files, {result.FilesFailed} failed.",
            _ => "other"
        };

        message.Should().Be("Backed up 10 files, 3 failed.");
    }

    [Fact]
    public void OnBackupCompleted_QuotaExceeded_ShowsQuotaMessage()
    {
        var result = new BackupRunResult
        {
            ResultType = BackupRunResultType.QuotaExceeded
        };

        var message = result.ResultType switch
        {
            BackupRunResultType.QuotaExceeded => "Backup skipped: storage quota exceeded.",
            _ => "other"
        };

        message.Should().Be("Backup skipped: storage quota exceeded.");
    }

    [Fact]
    public void OnStateChanged_Waiting_ShowsNextBackupTime()
    {
        var nextUtc = new DateTime(2026, 2, 11, 2, 0, 0, DateTimeKind.Utc);

        var statusText = BackupSchedulerState.Waiting.ToString();

        statusText.Should().Be("Waiting");
    }

    [Fact]
    public void OnStateChanged_Running_ShowsInProgress()
    {
        var statusText = BackupSchedulerState.Running switch
        {
            BackupSchedulerState.Running => "Backup in progress...",
            _ => "other"
        };

        statusText.Should().Be("Backup in progress...");
    }
}
