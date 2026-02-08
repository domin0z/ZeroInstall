using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// A structured report generated after a migration job completes.
/// Designed for local storage and future dashboard consumption.
/// </summary>
public class JobReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public string JobId { get; set; } = string.Empty;
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobStatus FinalStatus { get; set; }

    public string SourceHostname { get; set; } = string.Empty;
    public string DestinationHostname { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransportMethod TransportMethod { get; set; }

    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? DurationFormatted { get; set; }

    /// <summary>
    /// Summary counts by status.
    /// </summary>
    public JobReportSummary Summary { get; set; } = new();

    /// <summary>
    /// Per-item results.
    /// </summary>
    public List<JobReportItem> Items { get; set; } = [];

    /// <summary>
    /// Items that require manual attention from the technician.
    /// </summary>
    public List<string> ManualAttentionRequired { get; set; } = [];

    /// <summary>
    /// Errors encountered during migration.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Warnings encountered during migration.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

public class JobReportSummary
{
    public int TotalItems { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Warnings { get; set; }
    public long TotalBytesTransferred { get; set; }
}

public class JobReportItem
{
    public string ItemName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationItemType ItemType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationTier TierUsed { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationItemStatus Status { get; set; }

    public string? StatusMessage { get; set; }
    public long BytesTransferred { get; set; }
}
