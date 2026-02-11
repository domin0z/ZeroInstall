using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Configuration for domain migration operations on the destination machine.
/// </summary>
public class DomainMigrationConfiguration
{
    /// <summary>
    /// Target domain to join (e.g., "corp.local").
    /// </summary>
    public string? TargetDomain { get; set; }

    /// <summary>
    /// Target OU for the computer account (e.g., "OU=PCs,DC=corp,DC=local").
    /// </summary>
    public string? TargetOu { get; set; }

    /// <summary>
    /// New computer name to assign during domain join.
    /// </summary>
    public string? ComputerNewName { get; set; }

    /// <summary>
    /// Credentials for domain admin operations.
    /// </summary>
    public DomainCredentials DomainCredentials { get; set; } = new();

    /// <summary>
    /// Whether to join the machine to Azure AD instead of/in addition to AD.
    /// </summary>
    public bool JoinAzureAd { get; set; }

    /// <summary>
    /// Whether to set SID history on the new accounts for backward compatibility.
    /// </summary>
    public bool IncludeSidHistory { get; set; }

    /// <summary>
    /// Maps old username -> new username for profile reassignment.
    /// </summary>
    public Dictionary<string, string> UserLookupMap { get; set; } = new();

    /// <summary>
    /// Path to a PowerShell script to execute after migration completes.
    /// </summary>
    public string? PostMigrationScript { get; set; }

    /// <summary>
    /// What to do with old user accounts after migration.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PostMigrationAccountAction PostMigrationAccountAction { get; set; }
}
