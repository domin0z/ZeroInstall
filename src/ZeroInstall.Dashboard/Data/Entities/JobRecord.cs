using System.ComponentModel.DataAnnotations;

namespace ZeroInstall.Dashboard.Data.Entities;

public class JobRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string JobId { get; set; } = string.Empty;

    public string RawJson { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public string? SourceHostname { get; set; }
    public string? DestinationHostname { get; set; }
    public string? TechnicianName { get; set; }

    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ImportedUtc { get; set; } = DateTime.UtcNow;

    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public long TotalBytesTransferred { get; set; }
}
