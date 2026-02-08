using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// A single selectable item in the migration checklist presented to the technician.
/// Wraps a discovered application, user profile, system setting, or file group.
/// </summary>
public class MigrationItem
{
    /// <summary>
    /// Unique identifier for this item within the migration job.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationItemType ItemType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationTier RecommendedTier { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationTier? OverrideTier { get; set; }

    /// <summary>
    /// The effective tier (override if set, otherwise recommended).
    /// </summary>
    [JsonIgnore]
    public MigrationTier EffectiveTier => OverrideTier ?? RecommendedTier;

    /// <summary>
    /// Whether the technician has selected this item for migration.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Estimated size in bytes.
    /// </summary>
    public long EstimatedSizeBytes { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationItemStatus Status { get; set; } = MigrationItemStatus.Queued;

    /// <summary>
    /// Status message (e.g., error details, warnings).
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// The source data object. One of: DiscoveredApplication, UserProfile, SystemSetting.
    /// Stored as object for flexibility; consumers cast based on ItemType.
    /// </summary>
    [JsonIgnore]
    public object? SourceData { get; set; }
}
