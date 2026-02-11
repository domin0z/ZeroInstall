using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Creates and manages local user accounts on the destination machine.
/// Uses net.exe and PowerShell for account operations, registry for SID/profile lookups.
/// </summary>
public class UserAccountService : IUserAccountManager
{
    private const string ProfileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    private readonly IProcessRunner _processRunner;
    private readonly IRegistryAccessor _registry;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(
        IProcessRunner processRunner,
        IRegistryAccessor registry,
        ILogger<UserAccountService> logger)
    {
        _processRunner = processRunner;
        _registry = registry;
        _logger = logger;
    }

    public async Task<bool> UserExistsAsync(string username, CancellationToken ct = default)
    {
        var result = await _processRunner.RunAsync("net", $"user \"{username}\"", ct);
        return result.Success;
    }

    public async Task<string> CreateUserAsync(
        string username,
        string password,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var result = await _processRunner.RunAsync(
            "net", $"user \"{username}\" \"{password}\" /add", ct);

        if (!result.Success)
        {
            _logger.LogError("Failed to create user {Username}: {Error}", username, result.StandardError);
            throw new InvalidOperationException(
                $"Failed to create user '{username}': {result.StandardError}");
        }

        _logger.LogInformation("Created local user account: {Username}", username);

        if (isAdmin)
        {
            var adminResult = await _processRunner.RunAsync(
                "net", $"localgroup Administrators \"{username}\" /add", ct);

            if (!adminResult.Success)
            {
                _logger.LogWarning("Failed to add {Username} to Administrators: {Error}",
                    username, adminResult.StandardError);
            }
            else
            {
                _logger.LogInformation("Added {Username} to Administrators group", username);
            }
        }

        // Return the SID of the newly created user
        var sid = await GetUserSidAsync(username, ct);
        return sid ?? string.Empty;
    }

    public async Task<string?> GetUserSidAsync(string username, CancellationToken ct = default)
    {
        var script = $"(New-Object System.Security.Principal.NTAccount('{username}')).Translate(" +
                     "[System.Security.Principal.SecurityIdentifier]).Value";
        var result = await _processRunner.RunAsync(
            "powershell", $"-NoProfile -Command \"{script}\"", ct);

        if (!result.Success)
        {
            _logger.LogDebug("Failed to resolve SID for {Username}", username);
            return null;
        }

        return ParseSidFromOutput(result.StandardOutput);
    }

    public async Task<string?> GetUserProfilePathAsync(string username, CancellationToken ct = default)
    {
        // First try to look up via SID → registry ProfileImagePath
        var sid = await GetUserSidAsync(username, ct);
        if (!string.IsNullOrEmpty(sid))
        {
            var profilePath = _registry.GetStringValue(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                $@"{ProfileListKey}\{sid}", "ProfileImagePath");

            if (!string.IsNullOrEmpty(profilePath))
                return profilePath;
        }

        // Fallback: assume standard path
        var fallback = Path.Combine(@"C:\Users", username);
        _logger.LogDebug("Using fallback profile path for {Username}: {Path}", username, fallback);
        return fallback;
    }

    public Task<IReadOnlyList<UserProfile>> ListLocalUsersAsync(CancellationToken ct = default)
    {
        var profiles = new List<UserProfile>();

        var sids = _registry.GetSubKeyNames(
            RegistryHive.LocalMachine, RegistryView.Registry64, ProfileListKey);

        foreach (var sid in sids)
        {
            if (!sid.StartsWith("S-1-5-21-"))
                continue;

            var profilePath = _registry.GetStringValue(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                $@"{ProfileListKey}\{sid}", "ProfileImagePath");

            if (string.IsNullOrEmpty(profilePath))
                continue;

            var username = Path.GetFileName(profilePath);

            profiles.Add(new UserProfile
            {
                Username = username,
                Sid = sid,
                ProfilePath = profilePath,
                IsLocal = true
            });
        }

        _logger.LogDebug("Discovered {Count} local user profiles", profiles.Count);
        return Task.FromResult<IReadOnlyList<UserProfile>>(profiles);
    }

    private const string WinlogonKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

    public async Task<bool> DeleteUserAsync(string username, CancellationToken ct = default)
    {
        var result = await _processRunner.RunAsync("net", $"user \"{username}\" /delete", ct);

        if (result.Success)
        {
            _logger.LogInformation("Deleted user account: {Username}", username);
            return true;
        }

        _logger.LogError("Failed to delete user {Username}: {Error}", username, result.StandardError);
        return false;
    }

    public async Task<bool> DisableUserAsync(string username, CancellationToken ct = default)
    {
        var result = await _processRunner.RunAsync("net", $"user \"{username}\" /active:no", ct);

        if (result.Success)
        {
            _logger.LogInformation("Disabled user account: {Username}", username);
            return true;
        }

        _logger.LogError("Failed to disable user {Username}: {Error}", username, result.StandardError);
        return false;
    }

    public Task<bool> SetAutoLogonAsync(string username, string? password, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                // Clear auto-logon
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "AutoAdminLogon", "0");
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "DefaultUserName", "");
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "DefaultPassword", "");
                _logger.LogInformation("Auto-logon cleared");
            }
            else
            {
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "AutoAdminLogon", "1");
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "DefaultUserName", username);
                _registry.SetStringValue(
                    RegistryHive.LocalMachine, RegistryView.Registry64,
                    WinlogonKey, "DefaultPassword", password);
                _logger.LogInformation("Auto-logon configured for {Username}", username);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set auto-logon for {Username}", username);
            return Task.FromResult(false);
        }
    }

    internal static string? ParseSidFromOutput(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith("S-1-", StringComparison.Ordinal))
        {
            // Take the first line that looks like a SID
            var firstLine = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return firstLine.StartsWith("S-1-", StringComparison.Ordinal) ? firstLine : null;
        }

        return null;
    }
}
