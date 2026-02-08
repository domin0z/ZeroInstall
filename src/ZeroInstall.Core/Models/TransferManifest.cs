using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Describes what is being transferred and how, used to coordinate between source and destination.
/// Serialized to JSON and sent alongside the data payload.
/// </summary>
public class TransferManifest
{
    public string ManifestId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hostname of the source machine.
    /// </summary>
    public string SourceHostname { get; set; } = string.Empty;

    /// <summary>
    /// OS version of the source machine.
    /// </summary>
    public string SourceOsVersion { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransportMethod TransportMethod { get; set; }

    /// <summary>
    /// User mappings for this transfer (source user → destination user).
    /// </summary>
    public List<UserMapping> UserMappings { get; set; } = [];

    /// <summary>
    /// All items selected for migration.
    /// </summary>
    public List<MigrationItem> Items { get; set; } = [];

    /// <summary>
    /// Total estimated size of all selected items in bytes.
    /// </summary>
    public long TotalEstimatedSizeBytes => Items
        .Where(i => i.IsSelected)
        .Sum(i => i.EstimatedSizeBytes);

    /// <summary>
    /// Package install manifests for Tier 1 apps (winget/choco IDs).
    /// </summary>
    public List<PackageInstallEntry> PackageInstalls { get; set; } = [];

    /// <summary>
    /// Checksum of the manifest file itself for integrity verification.
    /// </summary>
    public string? ManifestChecksum { get; set; }
}

/// <summary>
/// An entry in the package install manifest for Tier 1 migration.
/// </summary>
public class PackageInstallEntry
{
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// "winget" or "chocolatey"
    /// </summary>
    public string PackageManager { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;
    public string? Version { get; set; }
}
