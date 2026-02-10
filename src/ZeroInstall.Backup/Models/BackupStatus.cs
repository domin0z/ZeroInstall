using ZeroInstall.Backup.Enums;

namespace ZeroInstall.Backup.Models;

/// <summary>
/// Status report uploaded to NAS for technician monitoring.
/// Written to {customerPath}/status/backup-status.json.
/// </summary>
public class BackupStatus
{
    /// <summary>
    /// Customer identifier.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Computer name where the agent is running.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Agent version string.
    /// </summary>
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Run ID of the most recent backup.
    /// </summary>
    public string? LastRunId { get; set; }

    /// <summary>
    /// Result of the most recent backup.
    /// </summary>
    public BackupRunResultType? LastRunResult { get; set; }

    /// <summary>
    /// UTC timestamp of the last completed backup.
    /// </summary>
    public DateTime? LastBackupUtc { get; set; }

    /// <summary>
    /// Files uploaded in the last run.
    /// </summary>
    public int LastFilesUploaded { get; set; }

    /// <summary>
    /// Bytes transferred in the last run.
    /// </summary>
    public long LastBytesTransferred { get; set; }

    /// <summary>
    /// UTC timestamp of the next scheduled file backup.
    /// </summary>
    public DateTime? NextScheduledUtc { get; set; }

    /// <summary>
    /// Total bytes used on NAS for this customer.
    /// </summary>
    public long NasUsageBytes { get; set; }

    /// <summary>
    /// Customer's quota in bytes (0 = unlimited).
    /// </summary>
    public long QuotaBytes { get; set; }

    /// <summary>
    /// UTC timestamp when this status was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
