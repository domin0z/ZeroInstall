using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Services;

public interface IDashboardDataService
{
    Task<JobRecord> UpsertJobAsync(MigrationJob job, CancellationToken ct = default);
    Task<JobRecord?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task<List<JobRecord>> ListJobsAsync(string? statusFilter = null, string? technicianFilter = null,
        int skip = 0, int take = 50, CancellationToken ct = default);
    Task<int> GetJobCountAsync(string? statusFilter = null, CancellationToken ct = default);

    Task<JobReportRecord> UpsertReportAsync(JobReport report, CancellationToken ct = default);
    Task<JobReportRecord?> GetReportByJobIdAsync(string jobId, CancellationToken ct = default);

    Task<BackupStatusRecord> UpsertBackupStatusAsync(string customerId, string machineName,
        string rawJson, CancellationToken ct = default);
    Task<List<BackupStatusRecord>> ListBackupStatusesAsync(CancellationToken ct = default);

    Task<AlertRecord> CreateAlertAsync(string alertType, string relatedId, string message, CancellationToken ct = default);
    Task<List<AlertRecord>> GetActiveAlertsAsync(CancellationToken ct = default);
    Task DismissAlertAsync(int alertId, CancellationToken ct = default);

    Task<DashboardStats> GetStatsAsync(CancellationToken ct = default);
}

public class DashboardStats
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int TotalCustomers { get; set; }
    public int HealthyBackups { get; set; }
    public int OverdueBackups { get; set; }
    public int ActiveAlerts { get; set; }
}
