using System.ComponentModel.DataAnnotations;

namespace ZeroInstall.Dashboard.Data.Entities;

public class BackupStatusRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    public string? MachineName { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public string? LastRunResult { get; set; }
    public DateTime? LastBackupUtc { get; set; }
    public DateTime? NextScheduledUtc { get; set; }

    public long NasUsageBytes { get; set; }
    public long QuotaBytes { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
