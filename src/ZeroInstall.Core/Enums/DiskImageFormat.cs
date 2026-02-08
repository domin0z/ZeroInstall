namespace ZeroInstall.Core.Enums;

/// <summary>
/// Supported disk image formats for full volume cloning (Tier 3).
/// </summary>
public enum DiskImageFormat
{
    /// <summary>
    /// Raw block-level image.
    /// </summary>
    Img,

    /// <summary>
    /// Sector-by-sector raw image.
    /// </summary>
    Raw,

    /// <summary>
    /// Hyper-V virtual hard disk (dynamically expanding).
    /// </summary>
    Vhdx
}
