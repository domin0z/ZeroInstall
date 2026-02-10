using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Executes a single file backup or full image backup run.
/// </summary>
internal interface IBackupExecutor
{
    /// <summary>
    /// Runs an incremental file backup: scan, diff, upload changed files, update index, report status.
    /// </summary>
    Task<BackupRunResult> RunFileBackupAsync(
        BackupConfiguration config,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a full image backup: clone volume, upload to NAS.
    /// </summary>
    Task<BackupRunResult> RunFullImageBackupAsync(
        BackupConfiguration config,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);
}
