using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Creates and manages local user accounts on the destination machine.
/// </summary>
public interface IUserAccountManager
{
    /// <summary>
    /// Checks whether a local user account exists on this machine.
    /// </summary>
    Task<bool> UserExistsAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Creates a new local user account.
    /// </summary>
    Task<string> CreateUserAsync(
        string username,
        string password,
        bool isAdmin = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the SID for an existing local user account.
    /// </summary>
    Task<string?> GetUserSidAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Gets the profile path for a user account (e.g., C:\Users\Username).
    /// </summary>
    Task<string?> GetUserProfilePathAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Lists all local user accounts on the machine.
    /// </summary>
    Task<IReadOnlyList<UserProfile>> ListLocalUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a local user account.
    /// </summary>
    Task<bool> DeleteUserAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Disables a local user account.
    /// </summary>
    Task<bool> DisableUserAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Configures auto-logon for a user account via registry.
    /// Pass null/empty password to clear auto-logon.
    /// </summary>
    Task<bool> SetAutoLogonAsync(string username, string? password, CancellationToken ct = default);
}
