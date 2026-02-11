using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Services;

public interface IAlertService
{
    Task EvaluateJobAsync(JobRecord job, CancellationToken ct = default);
    Task EvaluateBackupStatusAsync(BackupStatusRecord status, CancellationToken ct = default);
}
