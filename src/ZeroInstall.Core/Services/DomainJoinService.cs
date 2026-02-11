using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Domain join/unjoin/rename operations using PowerShell and dsregcmd.
/// </summary>
internal class DomainJoinService : IDomainJoinService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DomainJoinService> _logger;

    public DomainJoinService(IProcessRunner processRunner, ILogger<DomainJoinService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> JoinDomainAsync(
        string domain,
        string? ou,
        DomainCredentials credentials,
        string? newComputerName = null,
        CancellationToken ct = default)
    {
        try
        {
            var credPart = BuildCredentialPart(credentials);
            var ouPart = !string.IsNullOrWhiteSpace(ou) ? $" -OUPath '{ou}'" : "";
            var namePart = !string.IsNullOrWhiteSpace(newComputerName)
                ? $" -NewName '{newComputerName}'"
                : "";

            var command = $"Add-Computer -DomainName '{domain}'{ouPart} {credPart}{namePart} -Force";

            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{command}\"", ct);

            if (result.Success)
            {
                var msg = $"Successfully joined domain '{domain}'.";
                if (!string.IsNullOrWhiteSpace(newComputerName))
                    msg += $" Computer renamed to '{newComputerName}'.";
                msg += " A restart is required.";
                _logger.LogInformation(msg);
                return (true, msg);
            }

            var errorMsg = $"Failed to join domain '{domain}': {result.StandardError}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"Error joining domain: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    public async Task<(bool Success, string Message)> UnjoinDomainAsync(
        string? workgroupName = null,
        DomainCredentials? credentials = null,
        CancellationToken ct = default)
    {
        try
        {
            var wg = workgroupName ?? "WORKGROUP";
            var credPart = credentials is not null ? $" -UnjoinDomainCredential ({BuildCredentialPart(credentials)})" : "";
            var command = $"Remove-Computer{credPart} -WorkgroupName '{wg}' -Force";

            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{command}\"", ct);

            if (result.Success)
            {
                var msg = $"Successfully unjoined from domain. Workgroup set to '{wg}'. A restart is required.";
                _logger.LogInformation(msg);
                return (true, msg);
            }

            var errorMsg = $"Failed to unjoin domain: {result.StandardError}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"Error unjoining domain: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    public async Task<(bool Success, string Message)> JoinAzureAdAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _processRunner.RunAsync("dsregcmd", "/join", ct);

            if (result.Success)
            {
                var msg = "Azure AD join initiated. User may need to complete sign-in.";
                _logger.LogInformation(msg);
                return (true, msg);
            }

            var errorMsg = $"Azure AD join failed: {result.StandardError}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"Error joining Azure AD: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    public async Task<(bool Success, string Message)> RenameComputerAsync(
        string newName,
        DomainCredentials? credentials = null,
        CancellationToken ct = default)
    {
        try
        {
            var credPart = credentials is not null
                ? $" -DomainCredential ({BuildCredentialPart(credentials)})"
                : "";

            var command = $"Rename-Computer -NewName '{newName}'{credPart} -Force";

            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{command}\"", ct);

            if (result.Success)
            {
                var msg = $"Computer renamed to '{newName}'. A restart is required.";
                _logger.LogInformation(msg);
                return (true, msg);
            }

            var errorMsg = $"Failed to rename computer to '{newName}': {result.StandardError}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"Error renaming computer: {ex.Message}";
            _logger.LogError(ex, msg);
            return (false, msg);
        }
    }

    /// <summary>
    /// Builds the PowerShell credential expression.
    /// </summary>
    internal static string BuildCredentialPart(DomainCredentials creds)
    {
        return $"New-Object PSCredential('{creds.Domain}\\{creds.Username}', " +
               $"(ConvertTo-SecureString '{creds.Password}' -AsPlainText -Force))";
    }
}
