using System.ComponentModel.DataAnnotations;

namespace ZeroInstall.Dashboard.Data.Entities;

public class AlertRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string AlertType { get; set; } = string.Empty;

    public string? RelatedId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public DateTime? DismissedUtc { get; set; }
}
