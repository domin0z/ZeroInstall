namespace ZeroInstall.Core.Enums;

/// <summary>
/// The overall status of a migration job.
/// </summary>
public enum JobStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    PartialSuccess,
    Cancelled
}
