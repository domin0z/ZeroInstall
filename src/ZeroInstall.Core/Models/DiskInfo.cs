namespace ZeroInstall.Core.Models;

/// <summary>
/// Information about a physical disk, obtained via PowerShell Get-Disk.
/// </summary>
public class DiskInfo
{
    /// <summary>
    /// Disk number (0-based, as reported by Windows).
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Disk model/product name (e.g., "Samsung SSD 970 EVO Plus").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Total size of the disk in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Partition style: GPT, MBR, or RAW.
    /// </summary>
    public string PartitionStyle { get; set; } = string.Empty;

    /// <summary>
    /// Whether the disk is online and accessible.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Whether this is the system disk (contains the Windows boot partition).
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Whether this disk contains the active boot partition.
    /// </summary>
    public bool IsBoot { get; set; }

    /// <summary>
    /// Bus type: SATA, NVMe, USB, SCSI, etc.
    /// </summary>
    public string BusType { get; set; } = string.Empty;
}
