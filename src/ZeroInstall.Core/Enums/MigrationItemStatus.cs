namespace ZeroInstall.Core.Enums;

/// <summary>
/// The status of an individual item within a migration job.
/// </summary>
public enum MigrationItemStatus
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Skipped,
    Warning
}
