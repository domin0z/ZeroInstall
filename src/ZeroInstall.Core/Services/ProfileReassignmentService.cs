using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// In-place profile SID reassignment using registry surgery, icacls, and NTUSER.DAT rewrite.
/// </summary>
internal class ProfileReassignmentService : IProfileReassignmentService
{
    private const string ProfileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    private readonly IProcessRunner _processRunner;
    private readonly IRegistryAccessor _registry;
    private readonly ILogger<ProfileReassignmentService> _logger;

    public ProfileReassignmentService(
        IProcessRunner processRunner,
        IRegistryAccessor registry,
        ILogger<ProfileReassignmentService> logger)
    {
        _processRunner = processRunner;
        _registry = registry;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> ReassignProfileAsync(
        string oldSid,
        string newSid,
        string profilePath,
        CancellationToken ct = default)
    {
        try
        {
            // Step 1: Export old ProfileList key
            var tempRegFile = Path.Combine(Path.GetTempPath(), $"zim_profile_{oldSid}.reg");
            var exportResult = await _processRunner.RunAsync(
                "reg", $"export \"HKLM\\{ProfileListKey}\\{oldSid}\" \"{tempRegFile}\" /y", ct);

            if (!exportResult.Success)
                return (false, $"Failed to export registry key for SID {oldSid}: {exportResult.StandardError}");

            // Step 2: Replace old SID with new SID in exported file and import
            var importResult = await ImportWithSidReplace(tempRegFile, oldSid, newSid, ct);
            if (!importResult.Success)
                return (false, $"Failed to import registry key for new SID {newSid}: {importResult.StandardError}");

            // Step 3: Delete old SID key
            var deleteResult = await _processRunner.RunAsync(
                "reg", $"delete \"HKLM\\{ProfileListKey}\\{oldSid}\" /f", ct);

            if (!deleteResult.Success)
                _logger.LogWarning("Failed to delete old ProfileList key for {OldSid}: {Error}",
                    oldSid, deleteResult.StandardError);

            // Step 4: Grant new SID full control on profile folder
            var aclResult = await _processRunner.RunAsync(
                "icacls", $"\"{profilePath}\" /grant \"{newSid}:(OI)(CI)F\" /T /Q", ct);

            if (!aclResult.Success)
                _logger.LogWarning("Failed to set ACLs for {NewSid} on {Path}: {Error}",
                    newSid, profilePath, aclResult.StandardError);

            // Step 5: Set owner
            var ownerResult = await _processRunner.RunAsync(
                "icacls", $"\"{profilePath}\" /setowner \"{newSid}\" /T /Q", ct);

            if (!ownerResult.Success)
                _logger.LogWarning("Failed to set owner for {NewSid} on {Path}: {Error}",
                    newSid, profilePath, ownerResult.StandardError);

            // Step 6: Load NTUSER.DAT, replace SID references, unload
            var ntuserPath = Path.Combine(profilePath, "NTUSER.DAT");
            var hiveLoaded = false;
            try
            {
                var loadResult = await _processRunner.RunAsync(
                    "reg", $"load HKU\\TEMP_ZIM \"{ntuserPath}\"", ct);

                if (loadResult.Success)
                {
                    hiveLoaded = true;
                    // Export, replace SIDs, reimport
                    var hiveTempFile = Path.Combine(Path.GetTempPath(), "zim_ntuser_temp.reg");
                    await _processRunner.RunAsync(
                        "reg", $"export HKU\\TEMP_ZIM \"{hiveTempFile}\" /y", ct);

                    await ImportWithSidReplace(hiveTempFile, oldSid, newSid, ct);

                    try { File.Delete(hiveTempFile); }
                    catch { /* cleanup best-effort */ }
                }
                else
                {
                    _logger.LogWarning("Could not load NTUSER.DAT: {Error}", loadResult.StandardError);
                }
            }
            finally
            {
                if (hiveLoaded)
                {
                    await _processRunner.RunAsync("reg", "unload HKU\\TEMP_ZIM", ct);
                }
            }

            // Cleanup temp file
            try { File.Delete(tempRegFile); }
            catch { /* cleanup best-effort */ }

            var msg = $"Profile reassigned from {oldSid} to {newSid} at {profilePath}.";
            _logger.LogInformation(msg);
            return (true, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Error reassigning profile: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    public async Task<(bool Success, string Message)> RenameProfileFolderAsync(
        string currentPath,
        string newFolderName,
        CancellationToken ct = default)
    {
        try
        {
            var parentDir = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(parentDir))
                return (false, $"Cannot determine parent directory of '{currentPath}'.");

            var newPath = Path.Combine(parentDir, newFolderName);

            // Step 1: Rename the directory
            var renameResult = await _processRunner.RunAsync(
                "cmd", $"/c ren \"{currentPath}\" \"{newFolderName}\"", ct);

            if (!renameResult.Success)
                return (false, $"Failed to rename folder: {renameResult.StandardError}");

            // Step 2: Update registry ProfileImagePath for this profile
            var updated = UpdateProfileImagePath(currentPath, newPath);
            if (!updated)
                _logger.LogWarning("Could not update ProfileImagePath in registry for {Path}", currentPath);

            var msg = $"Profile folder renamed from '{Path.GetFileName(currentPath)}' to '{newFolderName}'.";
            _logger.LogInformation(msg);
            return (true, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Error renaming profile folder: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    public async Task<(bool Success, string Message)> SetSidHistoryAsync(
        string targetSid,
        string sourceSid,
        DomainCredentials credentials,
        CancellationToken ct = default)
    {
        try
        {
            if (!credentials.IsValid)
                return (false, "Domain credentials are required for SID history operations.");

            var credPart = DomainJoinService.BuildCredentialPart(credentials);
            var command = $"Set-ADUser -Identity (Get-ADUser -Filter {{SID -eq '{targetSid}'}}).SamAccountName " +
                          $"-Add @{{sIDHistory='{sourceSid}'}} " +
                          $"-Credential ({credPart})";

            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{command}\"", ct);

            if (result.Success)
            {
                var msg = $"SID history set: {sourceSid} added to {targetSid}.";
                _logger.LogInformation(msg);
                return (true, msg);
            }

            var errorMsg = $"Failed to set SID history: {result.StandardError}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"Error setting SID history: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    private async Task<ProcessResult> ImportWithSidReplace(
        string regFilePath, string oldSid, string newSid, CancellationToken ct)
    {
        // Read reg file, replace SID, write back, import
        var content = await File.ReadAllTextAsync(regFilePath, ct);
        content = content.Replace(oldSid, newSid);
        var replacedPath = regFilePath + ".replaced.reg";
        await File.WriteAllTextAsync(replacedPath, content, ct);

        var result = await _processRunner.RunAsync("reg", $"import \"{replacedPath}\"", ct);

        try { File.Delete(replacedPath); }
        catch { /* cleanup best-effort */ }

        return result;
    }

    private bool UpdateProfileImagePath(string oldPath, string newPath)
    {
        try
        {
            var sids = _registry.GetSubKeyNames(
                RegistryHive.LocalMachine, RegistryView.Registry64, ProfileListKey);

            foreach (var sid in sids)
            {
                var profileImagePath = _registry.GetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    $@"{ProfileListKey}\{sid}", "ProfileImagePath");

                if (string.Equals(profileImagePath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    _registry.SetStringValue(
                        RegistryHive.LocalMachine, RegistryView.Registry64,
                        $@"{ProfileListKey}\{sid}", "ProfileImagePath", newPath);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update ProfileImagePath from {Old} to {New}", oldPath, newPath);
            return false;
        }
    }
}
