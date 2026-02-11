namespace ZeroInstall.Core.Enums;

/// <summary>
/// Secure Boot status of the system firmware.
/// </summary>
public enum SecureBootStatus
{
    /// <summary>
    /// Status could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Secure Boot is enabled.
    /// </summary>
    Enabled,

    /// <summary>
    /// Secure Boot is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Secure Boot is not supported by this firmware (e.g., legacy BIOS).
    /// </summary>
    NotSupported
}
