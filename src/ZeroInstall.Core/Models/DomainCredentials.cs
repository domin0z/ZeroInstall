using System.Text.Json.Serialization;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Credentials for domain operations (join, unjoin, rename, SID history).
/// </summary>
public class DomainCredentials
{
    /// <summary>
    /// The domain name (e.g., "corp.local" or "CORP").
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The domain admin username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The domain admin password. Never serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// True if all required fields are populated.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Domain) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
