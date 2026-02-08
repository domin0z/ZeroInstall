namespace ZeroInstall.Core.Enums;

/// <summary>
/// The strategy used to migrate an application to the destination machine.
/// </summary>
public enum MigrationTier
{
    /// <summary>
    /// Tier 1: Clean install via winget/chocolatey, then overlay user settings and data.
    /// </summary>
    Package,

    /// <summary>
    /// Tier 2: Capture registry keys, Program Files, and AppData; replay on destination.
    /// </summary>
    RegistryFile,

    /// <summary>
    /// Tier 3: Full volume clone to .img/.raw/.vhdx; restore via WinPE.
    /// </summary>
    FullClone
}
