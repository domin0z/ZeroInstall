using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// In-place profile SID reassignment and folder rename for domain migration.
/// </summary>
public interface IProfileReassignmentService
{
    /// <summary>
    /// Reassigns a user profile from one SID to another in-place.
    /// This includes registry ProfileList key migration, ACL reassignment,
    /// and SID replacement in NTUSER.DAT.
    /// </summary>
    Task<(bool Success, string Message)> ReassignProfileAsync(
        string oldSid,
        string newSid,
        string profilePath,
        CancellationToken ct = default);

    /// <summary>
    /// Renames a user profile folder and updates the registry ProfileImagePath.
    /// </summary>
    Task<(bool Success, string Message)> RenameProfileFolderAsync(
        string currentPath,
        string newFolderName,
        CancellationToken ct = default);

    /// <summary>
    /// Sets SID history on a target AD account (requires RSAT + domain admin).
    /// </summary>
    Task<(bool Success, string Message)> SetSidHistoryAsync(
        string targetSid,
        string sourceSid,
        DomainCredentials credentials,
        CancellationToken ct = default);
}
