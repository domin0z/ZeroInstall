using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// A Windows user profile discovered on the source machine.
/// </summary>
public class UserProfile
{
    /// <summary>
    /// The Windows username (e.g., "Bill").
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The SID of this user account.
    /// </summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the profile directory (e.g., C:\Users\Bill).
    /// </summary>
    public string ProfilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a local account (vs. domain/Microsoft account).
    /// </summary>
    public bool IsLocal { get; set; }

    /// <summary>
    /// The domain or workgroup name for this account (e.g., "CORP", "AzureAD").
    /// Null for local accounts.
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// The type of account (Local, ActiveDirectory, AzureAd, MicrosoftAccount).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserAccountType AccountType { get; set; }

    /// <summary>
    /// Known folder paths within this profile.
    /// </summary>
    public UserProfileFolders Folders { get; set; } = new();

    /// <summary>
    /// Browser profiles detected for this user.
    /// </summary>
    public List<BrowserProfile> BrowserProfiles { get; set; } = [];

    /// <summary>
    /// Email client data paths detected for this user.
    /// </summary>
    public List<EmailClientData> EmailData { get; set; } = [];

    /// <summary>
    /// Estimated total size of the profile in bytes.
    /// </summary>
    public long EstimatedSizeBytes { get; set; }
}

/// <summary>
/// Standard known folder paths within a user profile.
/// </summary>
public class UserProfileFolders
{
    public string? Documents { get; set; }
    public string? Desktop { get; set; }
    public string? Downloads { get; set; }
    public string? Pictures { get; set; }
    public string? Music { get; set; }
    public string? Videos { get; set; }
    public string? Favorites { get; set; }
    public string? AppDataRoaming { get; set; }
    public string? AppDataLocal { get; set; }
    public string? AppDataLocalLow { get; set; }
}

/// <summary>
/// A browser profile detected within a user's AppData.
/// </summary>
public class BrowserProfile
{
    public string BrowserName { get; set; } = string.Empty;
    public string ProfilePath { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public long EstimatedSizeBytes { get; set; }
}

/// <summary>
/// Email client data detected within a user profile.
/// </summary>
public class EmailClientData
{
    public string ClientName { get; set; } = string.Empty;
    public List<string> DataPaths { get; set; } = [];
    public long EstimatedSizeBytes { get; set; }
}
