using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Represents a complete migration session from source to destination.
/// This is the top-level object that tracks the entire migration lifecycle.
/// </summary>
public class MigrationJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    /// Source machine hostname.
    /// </summary>
    public string SourceHostname { get; set; } = string.Empty;

    /// <summary>
    /// Source machine OS version string.
    /// </summary>
    public string SourceOsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Destination machine hostname.
    /// </summary>
    public string DestinationHostname { get; set; } = string.Empty;

    /// <summary>
    /// Destination machine OS version string.
    /// </summary>
    public string DestinationOsVersion { get; set; } = string.Empty;

    /// <summary>
    /// The technician who ran this migration.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the migration profile/template used, if any.
    /// </summary>
    public string? ProfileName { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransportMethod TransportMethod { get; set; }

    /// <summary>
    /// Domain info from the source machine.
    /// </summary>
    public DomainInfo? SourceDomainInfo { get; set; }

    /// <summary>
    /// Domain info from the destination machine.
    /// </summary>
    public DomainInfo? DestinationDomainInfo { get; set; }

    /// <summary>
    /// Domain migration configuration, if applicable.
    /// </summary>
    public DomainMigrationConfiguration? DomainMigrationConfig { get; set; }

    /// <summary>
    /// User mappings (source → destination user accounts).
    /// </summary>
    public List<UserMapping> UserMappings { get; set; } = [];

    /// <summary>
    /// All migration items (apps, profiles, settings) with their selection and status.
    /// </summary>
    public List<MigrationItem> Items { get; set; } = [];

    /// <summary>
    /// Duration of the migration.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => StartedUtc.HasValue && CompletedUtc.HasValue
        ? CompletedUtc.Value - StartedUtc.Value
        : StartedUtc.HasValue
            ? DateTime.UtcNow - StartedUtc.Value
            : null;
}
