using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Reports backup status and restore requests to the NAS.
/// </summary>
internal interface IStatusReporter
{
    /// <summary>
    /// Uploads backup status to the NAS.
    /// </summary>
    Task ReportStatusAsync(BackupConfiguration config, BackupStatus status, CancellationToken ct = default);

    /// <summary>
    /// Uploads a restore request to the NAS.
    /// </summary>
    Task SubmitRestoreRequestAsync(BackupConfiguration config, RestoreRequest request, CancellationToken ct = default);
}
