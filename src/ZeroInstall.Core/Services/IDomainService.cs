using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Detects domain/workgroup/Azure AD membership and classifies user accounts.
/// </summary>
public interface IDomainService
{
    /// <summary>
    /// Gets the domain/workgroup/Azure AD join status of this machine.
    /// </summary>
    Task<DomainInfo> GetDomainInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Classifies a user account by its SID (Local, AD, AzureAD, MicrosoftAccount).
    /// </summary>
    Task<UserAccountType> ClassifyUserAccountAsync(string sid, CancellationToken ct = default);

    /// <summary>
    /// Gets the domain part of a user account's NTAccount name (e.g., "CORP" from "CORP\jdoe").
    /// Returns null for local accounts.
    /// </summary>
    Task<string?> GetUserDomainAsync(string sid, CancellationToken ct = default);
}
