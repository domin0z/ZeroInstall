using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Aggregated firmware and system information for a machine.
/// </summary>
public class FirmwareInfo
{
    /// <summary>
    /// Type of firmware (BIOS or UEFI).
    /// </summary>
    public FirmwareType FirmwareType { get; set; } = FirmwareType.Unknown;

    /// <summary>
    /// Secure Boot status.
    /// </summary>
    public SecureBootStatus SecureBoot { get; set; } = SecureBootStatus.Unknown;

    /// <summary>
    /// Whether a TPM module is present.
    /// </summary>
    public bool TpmPresent { get; set; }

    /// <summary>
    /// TPM specification version (e.g., "2.0", "1.2"), or empty if no TPM.
    /// </summary>
    public string TpmVersion { get; set; } = string.Empty;

    /// <summary>
    /// BIOS vendor/manufacturer (e.g., "American Megatrends Inc.").
    /// </summary>
    public string BiosVendor { get; set; } = string.Empty;

    /// <summary>
    /// BIOS/firmware version string.
    /// </summary>
    public string BiosVersion { get; set; } = string.Empty;

    /// <summary>
    /// BIOS release date string.
    /// </summary>
    public string BiosReleaseDate { get; set; } = string.Empty;

    /// <summary>
    /// System manufacturer (e.g., "Dell Inc.", "Lenovo").
    /// </summary>
    public string SystemManufacturer { get; set; } = string.Empty;

    /// <summary>
    /// System model (e.g., "OptiPlex 7090", "ThinkPad T14s").
    /// </summary>
    public string SystemModel { get; set; } = string.Empty;

    /// <summary>
    /// BCD boot entries enumerated from the system.
    /// </summary>
    public List<BcdBootEntry> BootEntries { get; set; } = [];
}
