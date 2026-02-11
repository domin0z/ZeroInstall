using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Detects domain membership using WMI, dsregcmd, and nltest.
/// Classifies user accounts by SID-to-NTAccount translation.
/// </summary>
internal class DomainService : IDomainService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DomainService> _logger;

    public DomainService(IProcessRunner processRunner, ILogger<DomainService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<DomainInfo> GetDomainInfoAsync(CancellationToken ct = default)
    {
        var info = new DomainInfo();

        try
        {
            // Step 1: WMI for AD domain/workgroup
            var wmiCommand = "Get-CimInstance Win32_ComputerSystem | Select PartOfDomain, Domain, Workgroup | ConvertTo-Json";
            var wmiResult = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{wmiCommand}\"", ct);

            bool isAdJoined = false;
            if (wmiResult.Success && !string.IsNullOrWhiteSpace(wmiResult.StandardOutput))
            {
                var (partOfDomain, domain, workgroup) = ParseWmiDomainInfo(wmiResult.StandardOutput);
                isAdJoined = partOfDomain;
                info.DomainOrWorkgroup = partOfDomain ? domain : workgroup;
                info.RawOutput += $"WMI: {wmiResult.StandardOutput}\n";
            }

            // Step 2: dsregcmd for Azure AD status
            var dsregResult = await _processRunner.RunAsync("dsregcmd", "/status", ct);
            bool isAzureAdJoined = false;
            if (dsregResult.Success && !string.IsNullOrWhiteSpace(dsregResult.StandardOutput))
            {
                var (azureJoined, tenantName, tenantId, deviceId) = ParseDsregcmd(dsregResult.StandardOutput);
                isAzureAdJoined = azureJoined;
                info.AzureAdTenantName = tenantName;
                info.AzureAdTenantId = tenantId;
                info.AzureAdDeviceId = deviceId;
                info.RawOutput += $"dsregcmd: {dsregResult.StandardOutput}\n";
            }

            // Step 3: Determine join type
            if (isAdJoined && isAzureAdJoined)
                info.JoinType = DomainJoinType.HybridAzureAd;
            else if (isAdJoined)
                info.JoinType = DomainJoinType.ActiveDirectory;
            else if (isAzureAdJoined)
                info.JoinType = DomainJoinType.AzureAd;
            else
                info.JoinType = DomainJoinType.Workgroup;

            // Step 4: Get DC if AD-joined
            if (isAdJoined && !string.IsNullOrEmpty(info.DomainOrWorkgroup))
            {
                var nltestResult = await _processRunner.RunAsync(
                    "nltest", $"/dsgetdc:{info.DomainOrWorkgroup}", ct);
                if (nltestResult.Success && !string.IsNullOrWhiteSpace(nltestResult.StandardOutput))
                {
                    info.DomainController = ParseNltest(nltestResult.StandardOutput);
                    info.RawOutput += $"nltest: {nltestResult.StandardOutput}\n";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather domain information");
        }

        return info;
    }

    public async Task<UserAccountType> ClassifyUserAccountAsync(string sid, CancellationToken ct = default)
    {
        try
        {
            var script = $"(New-Object System.Security.Principal.SecurityIdentifier('{sid}')).Translate(" +
                         "[System.Security.Principal.NTAccount]).Value";
            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{script}\"", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                return UserAccountType.Unknown;

            var machineName = Environment.MachineName;
            return ParseNtAccount(result.StandardOutput.Trim(), machineName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to classify account for SID {Sid}", sid);
            return UserAccountType.Unknown;
        }
    }

    public async Task<string?> GetUserDomainAsync(string sid, CancellationToken ct = default)
    {
        try
        {
            var script = $"(New-Object System.Security.Principal.SecurityIdentifier('{sid}')).Translate(" +
                         "[System.Security.Principal.NTAccount]).Value";
            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{script}\"", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                return null;

            var ntAccount = result.StandardOutput.Trim();
            var parts = ntAccount.Split('\\', 2);
            if (parts.Length == 2)
            {
                var domain = parts[0];
                if (!string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    return domain;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get domain for SID {Sid}", sid);
            return null;
        }
    }

    /// <summary>
    /// Parses Win32_ComputerSystem JSON output for domain/workgroup info.
    /// </summary>
    internal static (bool PartOfDomain, string Domain, string Workgroup) ParseWmiDomainInfo(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var partOfDomain = root.TryGetProperty("PartOfDomain", out var pod)
                               && pod.ValueKind == JsonValueKind.True;

            var domain = root.TryGetProperty("Domain", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? string.Empty
                : string.Empty;

            var workgroup = root.TryGetProperty("Workgroup", out var w) && w.ValueKind == JsonValueKind.String
                ? w.GetString() ?? string.Empty
                : string.Empty;

            return (partOfDomain, domain, workgroup);
        }
        catch (JsonException)
        {
            return (false, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Parses dsregcmd /status output for Azure AD join info.
    /// </summary>
    internal static (bool AzureAdJoined, string? TenantName, string? TenantId, string? DeviceId) ParseDsregcmd(string output)
    {
        bool azureAdJoined = false;
        string? tenantName = null;
        string? tenantId = null;
        string? deviceId = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');

            if (line.StartsWith("AzureAdJoined", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetDsregValue(line);
                azureAdJoined = string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("TenantName", StringComparison.OrdinalIgnoreCase))
            {
                tenantName = GetDsregValue(line);
            }
            else if (line.StartsWith("TenantId", StringComparison.OrdinalIgnoreCase))
            {
                tenantId = GetDsregValue(line);
            }
            else if (line.StartsWith("DeviceId", StringComparison.OrdinalIgnoreCase))
            {
                deviceId = GetDsregValue(line);
            }
        }

        return (azureAdJoined, tenantName, tenantId, deviceId);
    }

    private static string? GetDsregValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0 || colonIndex >= line.Length - 1)
            return null;
        var value = line[(colonIndex + 1)..].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Parses nltest /dsgetdc output to extract domain controller name.
    /// </summary>
    internal static string? ParseNltest(string output)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');

            if (line.StartsWith("DC:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[3..].Trim().TrimStart('\\');
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        return null;
    }

    /// <summary>
    /// Classifies an NTAccount value (e.g., "CORP\jdoe") against the local machine name.
    /// </summary>
    internal static UserAccountType ParseNtAccount(string ntAccount, string machineName)
    {
        var parts = ntAccount.Split('\\', 2);
        if (parts.Length != 2)
            return UserAccountType.Unknown;

        var domain = parts[0];

        if (string.Equals(domain, machineName, StringComparison.OrdinalIgnoreCase))
            return UserAccountType.Local;

        if (string.Equals(domain, "AzureAD", StringComparison.OrdinalIgnoreCase))
            return UserAccountType.AzureAd;

        if (string.Equals(domain, "MicrosoftAccount", StringComparison.OrdinalIgnoreCase))
            return UserAccountType.MicrosoftAccount;

        // Any other domain prefix is treated as Active Directory
        return UserAccountType.ActiveDirectory;
    }
}
