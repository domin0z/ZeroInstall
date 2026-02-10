using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Scheduler state for the backup agent.
/// </summary>
internal enum BackupSchedulerState
{
    Idle,
    Running,
    Waiting
}

/// <summary>
/// Scheduler interface for the backup agent.
/// </summary>
internal interface IBackupScheduler
{
    /// <summary>
    /// Current scheduler state.
    /// </summary>
    BackupSchedulerState State { get; }

    /// <summary>
    /// Next scheduled backup time (UTC).
    /// </summary>
    DateTime? NextScheduledUtc { get; }

    /// <summary>
    /// Raised when the scheduler state changes.
    /// </summary>
    event Action<BackupSchedulerState>? StateChanged;

    /// <summary>
    /// Raised when a backup run completes.
    /// </summary>
    event Action<BackupRunResult>? BackupCompleted;

    /// <summary>
    /// Triggers an immediate backup run outside the normal schedule.
    /// </summary>
    Task TriggerBackupNowAsync(CancellationToken ct = default);
}
