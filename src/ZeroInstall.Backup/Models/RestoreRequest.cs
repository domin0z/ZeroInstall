using ZeroInstall.Backup.Enums;

namespace ZeroInstall.Backup.Models;

/// <summary>
/// Customer-initiated restore request, uploaded to NAS for technician review.
/// Written to {customerPath}/status/restore-request.json.
/// </summary>
public class RestoreRequest
{
    /// <summary>
    /// Customer identifier.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Computer name where the request originated.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Scope of the restore request.
    /// </summary>
    public RestoreScope Scope { get; set; } = RestoreScope.Full;

    /// <summary>
    /// Optional customer message describing what they need restored.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Specific file/folder paths to restore (for partial restores).
    /// </summary>
    public List<string> SpecificPaths { get; set; } = new();

    /// <summary>
    /// UTC timestamp when the request was created.
    /// </summary>
    public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
}
