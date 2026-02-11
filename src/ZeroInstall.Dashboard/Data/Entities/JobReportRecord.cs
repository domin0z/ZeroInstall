using System.ComponentModel.DataAnnotations;

namespace ZeroInstall.Dashboard.Data.Entities;

public class JobReportRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ReportId { get; set; } = string.Empty;

    [Required]
    public string JobId { get; set; } = string.Empty;

    public string RawJson { get; set; } = string.Empty;

    public string? FinalStatus { get; set; }
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
}
