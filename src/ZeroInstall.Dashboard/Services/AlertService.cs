using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Services;

internal class AlertService : IAlertService
{
    private readonly DashboardDbContext _db;
    private readonly ILogger<AlertService> _logger;

    private const string JobFailed = "JobFailed";
    private const string BackupOverdue = "BackupOverdue";
    private const string QuotaWarning = "QuotaWarning";

    public AlertService(DashboardDbContext db, ILogger<AlertService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EvaluateJobAsync(JobRecord job, CancellationToken ct = default)
    {
        if (job.Status == "Failed")
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(
                a => a.AlertType == JobFailed && a.RelatedId == job.JobId && a.IsActive, ct);

            if (existingAlert is null)
            {
                _db.Alerts.Add(new AlertRecord
                {
                    AlertType = JobFailed,
                    RelatedId = job.JobId,
                    Message = $"Migration job {job.JobId} failed ({job.SourceHostname} -> {job.DestinationHostname})",
                    IsActive = true
                });
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Created JobFailed alert for job {JobId}", job.JobId);
            }
        }
        else if (job.Status == "Completed")
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(
                a => a.AlertType == JobFailed && a.RelatedId == job.JobId && a.IsActive, ct);

            if (existingAlert is not null)
            {
                existingAlert.IsActive = false;
                existingAlert.DismissedUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Auto-dismissed JobFailed alert for job {JobId}", job.JobId);
            }
        }
    }

    public async Task EvaluateBackupStatusAsync(BackupStatusRecord status, CancellationToken ct = default)
    {
        var overdueThreshold = DateTime.UtcNow.AddHours(-48);

        // Overdue check
        var isOverdue = status.LastBackupUtc is null || status.LastBackupUtc < overdueThreshold;
        var existingOverdue = await _db.Alerts.FirstOrDefaultAsync(
            a => a.AlertType == BackupOverdue && a.RelatedId == status.CustomerId && a.IsActive, ct);

        if (isOverdue && existingOverdue is null)
        {
            _db.Alerts.Add(new AlertRecord
            {
                AlertType = BackupOverdue,
                RelatedId = status.CustomerId,
                Message = $"Backup overdue for customer {status.CustomerId} ({status.MachineName})",
                IsActive = true
            });
            _logger.LogInformation("Created BackupOverdue alert for customer {CustomerId}", status.CustomerId);
        }
        else if (!isOverdue && existingOverdue is not null)
        {
            existingOverdue.IsActive = false;
            existingOverdue.DismissedUtc = DateTime.UtcNow;
            _logger.LogInformation("Auto-dismissed BackupOverdue alert for customer {CustomerId}", status.CustomerId);
        }

        // Quota check (>90%)
        var isQuotaWarning = status.QuotaBytes > 0
            && (double)status.NasUsageBytes / status.QuotaBytes > 0.9;
        var existingQuota = await _db.Alerts.FirstOrDefaultAsync(
            a => a.AlertType == QuotaWarning && a.RelatedId == status.CustomerId && a.IsActive, ct);

        if (isQuotaWarning && existingQuota is null)
        {
            var percent = (int)((double)status.NasUsageBytes / status.QuotaBytes * 100);
            _db.Alerts.Add(new AlertRecord
            {
                AlertType = QuotaWarning,
                RelatedId = status.CustomerId,
                Message = $"Storage quota at {percent}% for customer {status.CustomerId}",
                IsActive = true
            });
            _logger.LogInformation("Created QuotaWarning alert for customer {CustomerId}", status.CustomerId);
        }
        else if (!isQuotaWarning && existingQuota is not null)
        {
            existingQuota.IsActive = false;
            existingQuota.DismissedUtc = DateTime.UtcNow;
            _logger.LogInformation("Auto-dismissed QuotaWarning alert for customer {CustomerId}", status.CustomerId);
        }

        await _db.SaveChangesAsync(ct);
    }
}
