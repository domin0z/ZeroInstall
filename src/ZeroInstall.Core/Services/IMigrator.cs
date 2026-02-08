using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Base interface for all migration strategies (Tier 1, 2, 3).
/// </summary>
public interface IMigrator
{
    /// <summary>
    /// Captures data from the source machine for the given migration items.
    /// </summary>
    Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Replays/restores captured data to the destination machine.
    /// </summary>
    Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Tier 1: Installs applications via winget/chocolatey, then overlays user settings.
/// </summary>
public interface IPackageMigrator : IMigrator
{
    /// <summary>
    /// Checks which discovered applications have available package manager entries.
    /// </summary>
    Task<IReadOnlyList<PackageInstallEntry>> ResolvePackagesAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        CancellationToken ct = default);

    /// <summary>
    /// Installs packages on the destination machine from the install manifest.
    /// </summary>
    Task InstallPackagesAsync(
        IReadOnlyList<PackageInstallEntry> packages,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Tier 2: Captures and replays registry keys, Program Files, and AppData.
/// </summary>
public interface IRegistryMigrator : IMigrator
{
    /// <summary>
    /// Exports registry keys associated with the given applications.
    /// </summary>
    Task ExportRegistryAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        string outputPath,
        CancellationToken ct = default);

    /// <summary>
    /// Imports previously exported registry keys to the destination.
    /// </summary>
    Task ImportRegistryAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        CancellationToken ct = default);
}

/// <summary>
/// Tier 3: Full volume cloning to .img/.raw/.vhdx.
/// </summary>
public interface IDiskCloner : IMigrator
{
    /// <summary>
    /// Clones an entire volume to an image file.
    /// </summary>
    Task CloneVolumeAsync(
        string volumePath,
        string outputImagePath,
        Enums.DiskImageFormat format,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Restores an image file to a target volume.
    /// </summary>
    Task RestoreImageAsync(
        string imagePath,
        string targetVolumePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies the integrity of an image file.
    /// </summary>
    Task<bool> VerifyImageAsync(string imagePath, CancellationToken ct = default);
}
