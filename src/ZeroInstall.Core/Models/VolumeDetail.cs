using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Information about a volume/partition, obtained via PowerShell Get-Volume.
/// </summary>
public class VolumeDetail
{
    /// <summary>
    /// Drive letter (e.g., "C"), or empty for unlettered volumes.
    /// </summary>
    public string DriveLetter { get; set; } = string.Empty;

    /// <summary>
    /// Volume label (e.g., "Windows", "Recovery").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// File system type (NTFS, FAT32, ReFS, etc.).
    /// </summary>
    public string FileSystem { get; set; } = string.Empty;

    /// <summary>
    /// Total size of the volume in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Free space on the volume in bytes.
    /// </summary>
    public long FreeSpaceBytes { get; set; }

    /// <summary>
    /// Physical disk number this volume resides on.
    /// </summary>
    public int DiskNumber { get; set; }

    /// <summary>
    /// Volume type (e.g., "Partition", "Simple").
    /// </summary>
    public string VolumeType { get; set; } = string.Empty;

    /// <summary>
    /// Health status (e.g., "Healthy", "At Risk").
    /// </summary>
    public string HealthStatus { get; set; } = string.Empty;

    /// <summary>
    /// BitLocker protection status for this volume.
    /// </summary>
    public BitLockerProtectionStatus BitLockerStatus { get; set; } = BitLockerProtectionStatus.Unknown;

    /// <summary>
    /// Whether this volume is BitLocker-encrypted (locked, unlocked, or suspended).
    /// </summary>
    public bool IsBitLockerEncrypted => BitLockerStatus is BitLockerProtectionStatus.Unlocked
        or BitLockerProtectionStatus.Locked
        or BitLockerProtectionStatus.Suspended;

    /// <summary>
    /// Lock status string from BitLocker (e.g., "Locked", "Unlocked").
    /// </summary>
    public string BitLockerLockStatus { get; set; } = string.Empty;
}
