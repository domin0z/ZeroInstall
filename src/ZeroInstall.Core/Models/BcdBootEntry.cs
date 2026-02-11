namespace ZeroInstall.Core.Models;

/// <summary>
/// A single entry from the Windows Boot Configuration Data (BCD) store.
/// </summary>
public class BcdBootEntry
{
    /// <summary>
    /// BCD identifier (e.g., "{current}", "{bootmgr}", "{default}").
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Entry type (e.g., "Windows Boot Manager", "Windows Boot Loader", "Firmware Application").
    /// </summary>
    public string EntryType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description (e.g., "Windows 10", "Windows Recovery Environment").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Device path (e.g., "partition=C:").
    /// </summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>
    /// Boot file path (e.g., "\Windows\system32\winload.efi").
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the default boot entry (identifier contains "{current}").
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Additional key-value properties from the BCD entry.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];
}
