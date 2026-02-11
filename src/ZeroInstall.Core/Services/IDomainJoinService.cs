using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Joins/unjoins machines to domains and renames computers.
/// </summary>
public interface IDomainJoinService
{
    /// <summary>
    /// Joins the machine to an Active Directory domain.
    /// </summary>
    Task<(bool Success, string Message)> JoinDomainAsync(
        string domain,
        string? ou,
        DomainCredentials credentials,
        string? newComputerName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Unjoins the machine from a domain back to a workgroup.
    /// </summary>
    Task<(bool Success, string Message)> UnjoinDomainAsync(
        string? workgroupName = null,
        DomainCredentials? credentials = null,
        CancellationToken ct = default);

    /// <summary>
    /// Initiates Azure AD device join (requires user context).
    /// </summary>
    Task<(bool Success, string Message)> JoinAzureAdAsync(CancellationToken ct = default);

    /// <summary>
    /// Renames the computer.
    /// </summary>
    Task<(bool Success, string Message)> RenameComputerAsync(
        string newName,
        DomainCredentials? credentials = null,
        CancellationToken ct = default);
}
