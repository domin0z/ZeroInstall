using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// File-based JSON implementation of <see cref="IJobLogger"/>.
/// Persists jobs to {basePath}/jobs/ and reports to {basePath}/reports/.
/// </summary>
public class JsonJobLogger : IJobLogger
{
    private readonly string _jobsPath;
    private readonly string _reportsPath;
    private readonly ILogger<JsonJobLogger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonJobLogger(string basePath, ILogger<JsonJobLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        _jobsPath = Path.Combine(basePath, "jobs");
        _reportsPath = Path.Combine(basePath, "reports");
        _logger = logger;

        Directory.CreateDirectory(_jobsPath);
        Directory.CreateDirectory(_reportsPath);
    }

    public async Task<MigrationJob> CreateJobAsync(MigrationJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.JobId))
            job.JobId = Guid.NewGuid().ToString("N");

        var filePath = GetJobFilePath(job.JobId);
        var json = JsonSerializer.Serialize(job, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        _logger.LogInformation("Created job {JobId}", job.JobId);
        return job;
    }

    public async Task UpdateJobAsync(MigrationJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var filePath = GetJobFilePath(job.JobId);
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"Job '{job.JobId}' does not exist.");

        var json = JsonSerializer.Serialize(job, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        _logger.LogDebug("Updated job {JobId}, status={Status}", job.JobId, job.Status);
    }

    public async Task<MigrationJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        var filePath = GetJobFilePath(jobId);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<MigrationJob>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<MigrationJob>> ListJobsAsync(
        JobStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        var jobs = new List<MigrationJob>();

        if (!Directory.Exists(_jobsPath))
            return jobs.AsReadOnly();

        foreach (var file in Directory.GetFiles(_jobsPath, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var job = JsonSerializer.Deserialize<MigrationJob>(json, JsonOptions);
                if (job is not null)
                    jobs.Add(job);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize job file {File}", file);
            }
        }

        if (statusFilter.HasValue)
            jobs.RemoveAll(j => j.Status != statusFilter.Value);

        return jobs
            .OrderByDescending(j => j.CreatedUtc)
            .ToList()
            .AsReadOnly();
    }

    public async Task<JobReport> GenerateReportAsync(string jobId, CancellationToken ct = default)
    {
        var job = await GetJobAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

        var report = new JobReport
        {
            JobId = job.JobId,
            FinalStatus = job.Status,
            SourceHostname = job.SourceHostname,
            DestinationHostname = job.DestinationHostname,
            TechnicianName = job.TechnicianName,
            TransportMethod = job.TransportMethod,
            StartedUtc = job.StartedUtc,
            CompletedUtc = job.CompletedUtc,
            DurationFormatted = job.Duration?.ToString(@"hh\:mm\:ss"),
            Summary = new JobReportSummary
            {
                TotalItems = job.Items.Count,
                Completed = job.Items.Count(i => i.Status == MigrationItemStatus.Completed),
                Failed = job.Items.Count(i => i.Status == MigrationItemStatus.Failed),
                Skipped = job.Items.Count(i => i.Status == MigrationItemStatus.Skipped),
                Warnings = job.Items.Count(i => i.Status == MigrationItemStatus.Warning),
                TotalBytesTransferred = job.Items
                    .Where(i => i.Status == MigrationItemStatus.Completed)
                    .Sum(i => i.EstimatedSizeBytes)
            },
            Items = job.Items.Select(i => new JobReportItem
            {
                ItemName = i.DisplayName,
                ItemType = i.ItemType,
                TierUsed = i.EffectiveTier,
                Status = i.Status,
                StatusMessage = i.StatusMessage,
                BytesTransferred = i.Status == MigrationItemStatus.Completed ? i.EstimatedSizeBytes : 0
            }).ToList(),
            Errors = job.Items
                .Where(i => i.Status == MigrationItemStatus.Failed && i.StatusMessage is not null)
                .Select(i => $"{i.DisplayName}: {i.StatusMessage}")
                .ToList(),
            Warnings = job.Items
                .Where(i => i.Status == MigrationItemStatus.Warning && i.StatusMessage is not null)
                .Select(i => $"{i.DisplayName}: {i.StatusMessage}")
                .ToList()
        };

        // Persist the report
        var reportPath = Path.Combine(_reportsPath, $"{report.ReportId}.json");
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(reportPath, json, ct);

        _logger.LogInformation("Generated report {ReportId} for job {JobId}", report.ReportId, jobId);
        return report;
    }

    public async Task ExportReportAsync(JobReport report, string outputPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputPath);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json, ct);

        _logger.LogInformation("Exported report {ReportId} to {Path}", report.ReportId, outputPath);
    }

    public Task PushReportToNasAsync(JobReport report, CancellationToken ct = default)
    {
        // NAS push is a future enhancement — log and return
        _logger.LogWarning("NAS push not yet configured. Report {ReportId} was not pushed.", report.ReportId);
        return Task.CompletedTask;
    }

    private string GetJobFilePath(string jobId) =>
        Path.Combine(_jobsPath, $"{jobId}.json");
}
