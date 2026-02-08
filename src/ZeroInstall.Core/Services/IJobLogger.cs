using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Tracks migration jobs and produces structured reports.
/// </summary>
public interface IJobLogger
{
    /// <summary>
    /// Creates and persists a new migration job.
    /// </summary>
    Task<MigrationJob> CreateJobAsync(MigrationJob job, CancellationToken ct = default);

    /// <summary>
    /// Updates the status and details of an existing job.
    /// </summary>
    Task UpdateJobAsync(MigrationJob job, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a job by ID.
    /// </summary>
    Task<MigrationJob?> GetJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Lists all jobs, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<MigrationJob>> ListJobsAsync(
        Enums.JobStatus? statusFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a structured report for a completed job.
    /// </summary>
    Task<JobReport> GenerateReportAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Exports a job report to a JSON file at the specified path.
    /// </summary>
    Task ExportReportAsync(JobReport report, string outputPath, CancellationToken ct = default);

    /// <summary>
    /// Pushes a job report to the configured NAS share path.
    /// </summary>
    Task PushReportToNasAsync(JobReport report, CancellationToken ct = default);
}
