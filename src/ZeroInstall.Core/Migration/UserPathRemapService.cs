using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Rewrites file paths, shortcuts, registry values, and config files
/// when the source and destination usernames differ.
/// Handles .lnk, .url, config files, pinned taskbar items, recent files,
/// MRU registry entries, and environment variables.
/// </summary>
public class UserPathRemapService : IUserPathRemapper
{
    private static readonly string[] ConfigExtensions =
        [".ini", ".xml", ".json", ".cfg", ".config", ".yaml", ".yml", ".toml"];

    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly IRegistryAccessor _registry;
    private readonly ILogger<UserPathRemapService> _logger;

    public UserPathRemapService(
        IProcessRunner processRunner,
        IFileSystemAccessor fileSystem,
        IRegistryAccessor registry,
        ILogger<UserPathRemapService> logger)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _registry = registry;
        _logger = logger;
    }

    public async Task RemapPathsAsync(
        UserMapping mapping,
        string targetDirectory,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        if (!mapping.RequiresPathRemapping)
        {
            _logger.LogDebug("No path remapping needed for {User}", mapping.DestinationUsername);
            return;
        }

        statusProgress?.Report($"Remapping shortcuts for {mapping.DestinationUsername}");
        await RemapShortcutsAsync(mapping, targetDirectory, ct);

        statusProgress?.Report($"Remapping URL files for {mapping.DestinationUsername}");
        await RemapUrlFilesAsync(mapping, targetDirectory, ct);

        statusProgress?.Report($"Remapping config files for {mapping.DestinationUsername}");
        await RemapConfigFilesAsync(mapping, targetDirectory, ct);

        statusProgress?.Report($"Remapping pinned taskbar items for {mapping.DestinationUsername}");
        await RemapPinnedTaskbarItemsAsync(mapping, ct);

        statusProgress?.Report($"Remapping recent files for {mapping.DestinationUsername}");
        await RemapRecentFilesAsync(mapping, ct);

        statusProgress?.Report($"Remapping registry paths for {mapping.DestinationUsername}");
        await RemapRegistryPathsAsync(mapping, statusProgress, ct);

        _logger.LogInformation("Completed path remapping for {Source} → {Dest}",
            mapping.SourceUser.Username, mapping.DestinationUsername);
    }

    public async Task RemapRegistryPathsAsync(
        UserMapping mapping,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        if (!mapping.RequiresPathRemapping) return;

        // Remap environment variables in HKCU\Environment
        await RemapEnvironmentVariablesAsync(mapping, ct);

        // Remap MRU entries (ComDlg32)
        await RemapMruRegistryEntriesAsync(mapping, ct);

        // Remap shell folder paths
        await RemapShellFolderRegistryAsync(mapping, ct);
    }

    public async Task RemapShortcutsAsync(
        UserMapping mapping,
        string searchDirectory,
        CancellationToken ct = default)
    {
        if (!_fileSystem.DirectoryExists(searchDirectory)) return;

        string[] lnkFiles;
        try
        {
            lnkFiles = _fileSystem.GetFiles(searchDirectory, "*.lnk", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate .lnk files in {Dir}", searchDirectory);
            return;
        }

        foreach (var lnkFile in lnkFiles)
        {
            ct.ThrowIfCancellationRequested();
            await RemapLnkFileAsync(lnkFile, mapping, ct);
        }

        _logger.LogDebug("Remapped {Count} shortcut files in {Dir}", lnkFiles.Length, searchDirectory);
    }

    public async Task RemapConfigFilesAsync(
        UserMapping mapping,
        string searchDirectory,
        CancellationToken ct = default)
    {
        if (!_fileSystem.DirectoryExists(searchDirectory)) return;

        foreach (var ext in ConfigExtensions)
        {
            string[] files;
            try
            {
                files = _fileSystem.GetFiles(searchDirectory, $"*{ext}", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to enumerate {Ext} files in {Dir}", ext, searchDirectory);
                continue;
            }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                await RemapConfigFileAsync(file, mapping, ct);
            }
        }
    }

    internal async Task RemapLnkFileAsync(string lnkPath, UserMapping mapping, CancellationToken ct)
    {
        var srcPrefix = mapping.SourcePathPrefix;
        var destPrefix = mapping.DestinationProfilePath;

        // Use PowerShell WScript.Shell COM to read and rewrite the shortcut
        var script = $@"
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut('{lnkPath.Replace("'", "''")}')
$changed = $false
if ($lnk.TargetPath -like '{srcPrefix.Replace("'", "''")}*') {{
    $lnk.TargetPath = $lnk.TargetPath -replace [regex]::Escape('{srcPrefix}'), '{destPrefix}'
    $changed = $true
}}
if ($lnk.WorkingDirectory -like '{srcPrefix.Replace("'", "''")}*') {{
    $lnk.WorkingDirectory = $lnk.WorkingDirectory -replace [regex]::Escape('{srcPrefix}'), '{destPrefix}'
    $changed = $true
}}
if ($changed) {{ $lnk.Save() }}
Write-Output $changed";

        try
        {
            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{script.Replace("\"", "\\\"").Replace("\n", " ")}\"", ct);

            if (result.Success && result.StandardOutput.Trim().Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Remapped shortcut: {Path}", lnkPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remap shortcut {Path}", lnkPath);
        }
    }

    internal async Task RemapUrlFilesAsync(UserMapping mapping, string searchDirectory, CancellationToken ct)
    {
        if (!_fileSystem.DirectoryExists(searchDirectory)) return;

        string[] urlFiles;
        try
        {
            urlFiles = _fileSystem.GetFiles(searchDirectory, "*.url", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate .url files in {Dir}", searchDirectory);
            return;
        }

        foreach (var urlFile in urlFiles)
        {
            ct.ThrowIfCancellationRequested();
            await RemapUrlFileAsync(urlFile, mapping, ct);
        }
    }

    internal async Task RemapUrlFileAsync(string urlPath, UserMapping mapping, CancellationToken ct)
    {
        try
        {
            var content = await File.ReadAllTextAsync(urlPath, ct);
            if (!ContainsUserPath(content, mapping.SourcePathPrefix)) return;

            var remapped = ReplacePathPrefix(content, mapping.SourcePathPrefix, mapping.DestinationProfilePath);
            await File.WriteAllTextAsync(urlPath, remapped, ct);
            _logger.LogDebug("Remapped URL file: {Path}", urlPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remap URL file {Path}", urlPath);
        }
    }

    private async Task RemapConfigFileAsync(string filePath, UserMapping mapping, CancellationToken ct)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (!ContainsUserPath(content, mapping.SourcePathPrefix)) return;

            var remapped = ReplacePathPrefix(content, mapping.SourcePathPrefix, mapping.DestinationProfilePath);
            await File.WriteAllTextAsync(filePath, remapped, ct);
            _logger.LogDebug("Remapped config file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remap config file {Path}", filePath);
        }
    }

    internal async Task RemapPinnedTaskbarItemsAsync(UserMapping mapping, CancellationToken ct)
    {
        var taskbarDir = Path.Combine(
            mapping.DestinationProfilePath, "AppData", "Roaming",
            "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

        if (_fileSystem.DirectoryExists(taskbarDir))
        {
            await RemapShortcutsAsync(mapping, taskbarDir, ct);
        }
    }

    internal async Task RemapRecentFilesAsync(UserMapping mapping, CancellationToken ct)
    {
        var recentDir = Path.Combine(
            mapping.DestinationProfilePath, "AppData", "Roaming",
            "Microsoft", "Windows", "Recent");

        if (_fileSystem.DirectoryExists(recentDir))
        {
            await RemapShortcutsAsync(mapping, recentDir, ct);
        }
    }

    internal async Task RemapMruRegistryEntriesAsync(UserMapping mapping, CancellationToken ct)
    {
        var mruKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32";
        var tempFile = Path.Combine(Path.GetTempPath(), $"zim-mru-{Guid.NewGuid():N}.reg");

        try
        {
            // Export ComDlg32 MRU entries
            var exportResult = await _processRunner.RunAsync(
                "reg", $"export \"{mruKey}\" \"{tempFile}\" /y", ct);

            if (!exportResult.Success) return;

            // Read and remap paths
            var content = await File.ReadAllTextAsync(tempFile, ct);

            // Registry .reg files use double backslashes in path values
            var srcDoubleSlash = mapping.SourcePathPrefix.Replace("\\", "\\\\");
            if (!ContainsUserPath(content, mapping.SourcePathPrefix) &&
                !ContainsUserPath(content, srcDoubleSlash))
                return;
            var destDoubleSlash = mapping.DestinationProfilePath.Replace("\\", "\\\\");

            var remapped = content.Replace(srcDoubleSlash, destDoubleSlash, StringComparison.OrdinalIgnoreCase);
            remapped = ReplacePathPrefix(remapped, mapping.SourcePathPrefix, mapping.DestinationProfilePath);

            await File.WriteAllTextAsync(tempFile, remapped, ct);

            // Re-import
            await _processRunner.RunAsync("reg", $"import \"{tempFile}\"", ct);
            _logger.LogDebug("Remapped MRU registry entries for {User}", mapping.DestinationUsername);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remap MRU registry entries");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    internal async Task RemapEnvironmentVariablesAsync(UserMapping mapping, CancellationToken ct)
    {
        var envKey = @"Environment";

        var valueNames = _registry.GetValueNames(
            Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default, envKey);

        foreach (var name in valueNames)
        {
            var value = _registry.GetStringValue(
                Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default, envKey, name);

            if (string.IsNullOrEmpty(value) || !ContainsUserPath(value, mapping.SourcePathPrefix))
                continue;

            var remapped = ReplacePathPrefix(value, mapping.SourcePathPrefix, mapping.DestinationProfilePath);

            await _processRunner.RunAsync(
                "reg", $"add \"HKCU\\Environment\" /v \"{name}\" /t REG_SZ /d \"{remapped}\" /f", ct);

            _logger.LogDebug("Remapped environment variable {Name}: {Old} → {New}",
                name, value, remapped);
        }
    }

    private async Task RemapShellFolderRegistryAsync(UserMapping mapping, CancellationToken ct)
    {
        var shellFolderKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";
        var tempFile = Path.Combine(Path.GetTempPath(), $"zim-shell-{Guid.NewGuid():N}.reg");

        try
        {
            var exportResult = await _processRunner.RunAsync(
                "reg", $"export \"{shellFolderKey}\" \"{tempFile}\" /y", ct);

            if (!exportResult.Success) return;

            var content = await File.ReadAllTextAsync(tempFile, ct);
            var srcDoubleSlash = mapping.SourcePathPrefix.Replace("\\", "\\\\");

            if (!ContainsUserPath(content, mapping.SourcePathPrefix) &&
                !ContainsUserPath(content, srcDoubleSlash))
                return;

            var destDoubleSlash = mapping.DestinationProfilePath.Replace("\\", "\\\\");

            var remapped = content.Replace(srcDoubleSlash, destDoubleSlash, StringComparison.OrdinalIgnoreCase);
            remapped = ReplacePathPrefix(remapped, mapping.SourcePathPrefix, mapping.DestinationProfilePath);
            await File.WriteAllTextAsync(tempFile, remapped, ct);

            await _processRunner.RunAsync("reg", $"import \"{tempFile}\"", ct);
            _logger.LogDebug("Remapped Shell Folders registry for {User}", mapping.DestinationUsername);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remap Shell Folders registry");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    internal static string ReplacePathPrefix(string value, string sourcePrefix, string destPrefix)
    {
        return value.Replace(sourcePrefix, destPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsUserPath(string content, string prefix)
    {
        return content.Contains(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
