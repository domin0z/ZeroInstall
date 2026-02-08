using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Rewrites file paths, shortcuts, registry values, and config files
/// when the source and destination usernames differ.
/// </summary>
public interface IUserPathRemapper
{
    /// <summary>
    /// Remaps all path references for the given user mapping within the specified directory.
    /// Handles .lnk files, .url files, config files, and other known path-containing artifacts.
    /// </summary>
    Task RemapPathsAsync(
        UserMapping mapping,
        string targetDirectory,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Remaps user-profile paths in HKCU registry entries for the mapped user.
    /// </summary>
    Task RemapRegistryPathsAsync(
        UserMapping mapping,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Remaps paths in shortcut (.lnk) files within the specified directory tree.
    /// </summary>
    Task RemapShortcutsAsync(
        UserMapping mapping,
        string searchDirectory,
        CancellationToken ct = default);

    /// <summary>
    /// Remaps paths in known config file formats (INI, XML, JSON) within the specified directory.
    /// </summary>
    Task RemapConfigFilesAsync(
        UserMapping mapping,
        string searchDirectory,
        CancellationToken ct = default);
}
