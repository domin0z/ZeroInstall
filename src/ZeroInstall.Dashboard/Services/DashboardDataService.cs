using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Services;

internal class DashboardDataService : IDashboardDataService
{
    private readonly DashboardDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public DashboardDataService(DashboardDbContext db)
    {
        _db = db;
    }

    public async Task<JobRecord> UpsertJobAsync(MigrationJob job, CancellationToken ct = default)
    {
        var rawJson = JsonSerializer.Serialize(job, JsonOptions);
        var existing = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == job.JobId, ct);

        if (existing is not null)
        {
            existing.RawJson = rawJson;
            existing.Status = job.Status.ToString();
            existing.SourceHostname = job.SourceHostname;
            existing.DestinationHostname = job.DestinationHostname;
            existing.TechnicianName = job.TechnicianName;
            existing.StartedUtc = job.StartedUtc;
            existing.CompletedUtc = job.CompletedUtc;
            existing.TotalItems = job.Items.Count;
            existing.CompletedItems = job.Items.Count(i => i.Status == MigrationItemStatus.Completed);
            existing.FailedItems = job.Items.Count(i => i.Status == MigrationItemStatus.Failed);
            existing.TotalBytesTransferred = job.Items
                .Where(i => i.Status == MigrationItemStatus.Completed)
                .Sum(i => i.EstimatedSizeBytes);
            existing.ImportedUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new JobRecord
            {
                JobId = job.JobId,
                RawJson = rawJson,
                Status = job.Status.ToString(),
                SourceHostname = job.SourceHostname,
                DestinationHostname = job.DestinationHostname,
                TechnicianName = job.TechnicianName,
                StartedUtc = job.StartedUtc,
                CompletedUtc = job.CompletedUtc,
                CreatedUtc = job.CreatedUtc,
                ImportedUtc = DateTime.UtcNow,
                TotalItems = job.Items.Count,
                CompletedItems = job.Items.Count(i => i.Status == MigrationItemStatus.Completed),
                FailedItems = job.Items.Count(i => i.Status == MigrationItemStatus.Failed),
                TotalBytesTransferred = job.Items
                    .Where(i => i.Status == MigrationItemStatus.Completed)
                    .Sum(i => i.EstimatedSizeBytes)
            };
            _db.Jobs.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<JobRecord?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        return await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
    }

    public async Task<List<JobRecord>> ListJobsAsync(string? statusFilter = null,
        string? technicianFilter = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var query = _db.Jobs.AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(j => j.Status == statusFilter);
        if (!string.IsNullOrEmpty(technicianFilter))
            query = query.Where(j => j.TechnicianName != null && j.TechnicianName.Contains(technicianFilter));

        return await query
            .OrderByDescending(j => j.CreatedUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetJobCountAsync(string? statusFilter = null, CancellationToken ct = default)
    {
        var query = _db.Jobs.AsQueryable();
        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(j => j.Status == statusFilter);
        return await query.CountAsync(ct);
    }

    public async Task<JobReportRecord> UpsertReportAsync(JobReport report, CancellationToken ct = default)
    {
        var rawJson = JsonSerializer.Serialize(report, JsonOptions);
        var existing = await _db.Reports.FirstOrDefaultAsync(r => r.ReportId == report.ReportId, ct);

        if (existing is not null)
        {
            existing.RawJson = rawJson;
            existing.FinalStatus = report.FinalStatus.ToString();
            existing.GeneratedUtc = report.GeneratedUtc;
        }
        else
        {
            existing = new JobReportRecord
            {
                ReportId = report.ReportId,
                JobId = report.JobId,
                RawJson = rawJson,
                FinalStatus = report.FinalStatus.ToString(),
                GeneratedUtc = report.GeneratedUtc
            };
            _db.Reports.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<JobReportRecord?> GetReportByJobIdAsync(string jobId, CancellationToken ct = default)
    {
        return await _db.Reports.FirstOrDefaultAsync(r => r.JobId == jobId, ct);
    }

    public async Task<BackupStatusRecord> UpsertBackupStatusAsync(string customerId, string machineName,
        string rawJson, CancellationToken ct = default)
    {
        var existing = await _db.BackupStatuses.FirstOrDefaultAsync(b => b.CustomerId == customerId, ct);

        JsonElement? parsed = null;
        try { parsed = JsonDocument.Parse(rawJson).RootElement; } catch { }

        if (existing is not null)
        {
            existing.MachineName = machineName;
            existing.RawJson = rawJson;
            existing.UpdatedUtc = DateTime.UtcNow;
            if (parsed.HasValue)
            {
                existing.LastRunResult = parsed.Value.TryGetProperty("lastRunResult", out var lr) ? lr.ToString() : null;
                existing.LastBackupUtc = parsed.Value.TryGetProperty("lastBackupUtc", out var lb)
                    && lb.TryGetDateTime(out var lbDate) ? lbDate : existing.LastBackupUtc;
                existing.NextScheduledUtc = parsed.Value.TryGetProperty("nextScheduledUtc", out var ns)
                    && ns.TryGetDateTime(out var nsDate) ? nsDate : existing.NextScheduledUtc;
                existing.NasUsageBytes = parsed.Value.TryGetProperty("nasUsageBytes", out var nu)
                    && nu.TryGetInt64(out var nuVal) ? nuVal : existing.NasUsageBytes;
                existing.QuotaBytes = parsed.Value.TryGetProperty("quotaBytes", out var qb)
                    && qb.TryGetInt64(out var qbVal) ? qbVal : existing.QuotaBytes;
            }
        }
        else
        {
            existing = new BackupStatusRecord
            {
                CustomerId = customerId,
                MachineName = machineName,
                RawJson = rawJson,
                UpdatedUtc = DateTime.UtcNow
            };
            if (parsed.HasValue)
            {
                existing.LastRunResult = parsed.Value.TryGetProperty("lastRunResult", out var lr) ? lr.ToString() : null;
                existing.LastBackupUtc = parsed.Value.TryGetProperty("lastBackupUtc", out var lb)
                    && lb.TryGetDateTime(out var lbDate) ? lbDate : null;
                existing.NextScheduledUtc = parsed.Value.TryGetProperty("nextScheduledUtc", out var ns)
                    && ns.TryGetDateTime(out var nsDate) ? nsDate : null;
                existing.NasUsageBytes = parsed.Value.TryGetProperty("nasUsageBytes", out var nu)
                    && nu.TryGetInt64(out var nuVal) ? nuVal : 0;
                existing.QuotaBytes = parsed.Value.TryGetProperty("quotaBytes", out var qb)
                    && qb.TryGetInt64(out var qbVal) ? qbVal : 0;
            }
            _db.BackupStatuses.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<List<BackupStatusRecord>> ListBackupStatusesAsync(CancellationToken ct = default)
    {
        return await _db.BackupStatuses
            .OrderBy(b => b.CustomerId)
            .ToListAsync(ct);
    }

    public async Task<AlertRecord> CreateAlertAsync(string alertType, string relatedId, string message,
        CancellationToken ct = default)
    {
        var alert = new AlertRecord
        {
            AlertType = alertType,
            RelatedId = relatedId,
            Message = message,
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<List<AlertRecord>> GetActiveAlertsAsync(CancellationToken ct = default)
    {
        return await _db.Alerts
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.CreatedUtc)
            .ToListAsync(ct);
    }

    public async Task DismissAlertAsync(int alertId, CancellationToken ct = default)
    {
        var alert = await _db.Alerts.FindAsync(new object[] { alertId }, ct);
        if (alert is not null)
        {
            alert.IsActive = false;
            alert.DismissedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<DashboardStats> GetStatsAsync(CancellationToken ct = default)
    {
        var overdueThreshold = DateTime.UtcNow.AddHours(-48);

        return new DashboardStats
        {
            TotalJobs = await _db.Jobs.CountAsync(ct),
            ActiveJobs = await _db.Jobs.CountAsync(j => j.Status == "InProgress", ct),
            CompletedJobs = await _db.Jobs.CountAsync(j => j.Status == "Completed", ct),
            FailedJobs = await _db.Jobs.CountAsync(j => j.Status == "Failed", ct),
            TotalCustomers = await _db.BackupStatuses.CountAsync(ct),
            HealthyBackups = await _db.BackupStatuses.CountAsync(b =>
                b.LastBackupUtc != null && b.LastBackupUtc > overdueThreshold, ct),
            OverdueBackups = await _db.BackupStatuses.CountAsync(b =>
                b.LastBackupUtc == null || b.LastBackupUtc <= overdueThreshold, ct),
            ActiveAlerts = await _db.Alerts.CountAsync(a => a.IsActive, ct)
        };
    }
}
