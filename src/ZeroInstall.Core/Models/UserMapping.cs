namespace ZeroInstall.Core.Models;

/// <summary>
/// Maps a source user profile to a destination user account.
/// Handles username differences (e.g., source "Bill" → destination "William")
/// and drives the path remapping logic.
/// </summary>
public class UserMapping
{
    /// <summary>
    /// The user profile as discovered on the source machine.
    /// </summary>
    public UserProfile SourceUser { get; set; } = new();

    /// <summary>
    /// The username on the destination machine.
    /// May differ from the source username.
    /// </summary>
    public string DestinationUsername { get; set; } = string.Empty;

    /// <summary>
    /// The profile path on the destination machine (e.g., C:\Users\William).
    /// </summary>
    public string DestinationProfilePath { get; set; } = string.Empty;

    /// <summary>
    /// The SID of the destination user account.
    /// Populated after the account is created or matched.
    /// </summary>
    public string? DestinationSid { get; set; }

    /// <summary>
    /// Whether to create this user account on the destination if it doesn't exist.
    /// </summary>
    public bool CreateIfMissing { get; set; }

    /// <summary>
    /// Password for the new account (only used if CreateIfMissing is true).
    /// Not persisted to disk — used transiently during migration.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? NewAccountPassword { get; set; }

    /// <summary>
    /// The old user path prefix for remapping (e.g., "C:\Users\Bill").
    /// </summary>
    public string SourcePathPrefix => SourceUser.ProfilePath;

    /// <summary>
    /// Whether the source and destination usernames differ, requiring path remapping.
    /// </summary>
    public bool RequiresPathRemapping =>
        !string.Equals(SourceUser.ProfilePath, DestinationProfilePath, StringComparison.OrdinalIgnoreCase);
}
