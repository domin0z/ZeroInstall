namespace ZeroInstall.Backup.Enums;

/// <summary>
/// Outcome of a single backup run.
/// </summary>
public enum BackupRunResultType
{
    Success,
    PartialSuccess,
    Failed,
    QuotaExceeded,
    Skipped
}
