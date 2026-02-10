using Microsoft.Win32;

namespace ZeroInstall.App.Services;

/// <summary>
/// Production implementation of <see cref="IDialogService"/> using WPF dialogs.
/// </summary>
internal sealed class DialogService : IDialogService
{
    public Task<string?> BrowseFolderAsync(string? title = null, string? initialPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title ?? "Select Folder",
            Multiselect = false
        };

        if (!string.IsNullOrEmpty(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        var result = dialog.ShowDialog() == true ? dialog.FolderName : null;
        return Task.FromResult(result);
    }

    public Task<string?> BrowseFileAsync(string? filter = null, string? initialPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter ?? "All files (*.*)|*.*",
            Multiselect = false
        };

        if (!string.IsNullOrEmpty(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        var result = dialog.ShowDialog() == true ? dialog.FileName : null;
        return Task.FromResult(result);
    }
}
