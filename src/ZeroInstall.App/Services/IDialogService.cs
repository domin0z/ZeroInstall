namespace ZeroInstall.App.Services;

/// <summary>
/// Wraps OS file/folder dialogs for testability.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a folder browser dialog and returns the selected path, or null if cancelled.
    /// </summary>
    Task<string?> BrowseFolderAsync(string? title = null, string? initialPath = null);

    /// <summary>
    /// Shows a file browser dialog and returns the selected path, or null if cancelled.
    /// </summary>
    Task<string?> BrowseFileAsync(string? filter = null, string? initialPath = null);
}
