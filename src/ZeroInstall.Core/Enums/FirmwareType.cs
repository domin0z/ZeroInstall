namespace ZeroInstall.Core.Enums;

/// <summary>
/// Type of system firmware.
/// </summary>
public enum FirmwareType
{
    /// <summary>
    /// Firmware type could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Legacy BIOS firmware.
    /// </summary>
    Bios,

    /// <summary>
    /// UEFI firmware.
    /// </summary>
    Uefi
}
