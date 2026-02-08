using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// A saved migration template that pre-selects items and configures transfer preferences.
/// Stored as JSON on the technician's flash drive or pulled from NAS.
/// </summary>
public class MigrationProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "0.1.0";
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    public ProfileItemSelection Items { get; set; } = new();
    public ProfileTransportPreferences Transport { get; set; } = new();
}

public class ProfileItemSelection
{
    public ProfileUserProfileSettings UserProfiles { get; set; } = new();
    public ProfileApplicationSettings Applications { get; set; } = new();
    public ProfileBrowserSettings BrowserData { get; set; } = new();
    public ProfileSystemSettings SystemSettings { get; set; } = new();
}

public class ProfileUserProfileSettings
{
    public bool Enabled { get; set; } = true;
    public bool IncludeAll { get; set; } = true;
    public List<string> Folders { get; set; } = ["Documents", "Desktop", "Downloads", "Pictures", "Music", "Videos", "Favorites"];
}

public class ProfileApplicationSettings
{
    public bool Enabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationTier PreferredTier { get; set; } = MigrationTier.Package;

    /// <summary>
    /// App names or wildcard patterns to always include.
    /// </summary>
    public List<string> Include { get; set; } = [];

    /// <summary>
    /// App names or wildcard patterns to always exclude.
    /// </summary>
    public List<string> Exclude { get; set; } = [];
}

public class ProfileBrowserSettings
{
    public bool Enabled { get; set; } = true;
    public List<string> Browsers { get; set; } = ["Chrome", "Firefox", "Edge"];
    public bool IncludeBookmarks { get; set; } = true;
    public bool IncludeExtensions { get; set; } = true;
    public bool IncludePasswords { get; set; }
}

public class ProfileSystemSettings
{
    public bool Enabled { get; set; } = true;
    public bool WifiProfiles { get; set; } = true;
    public bool Printers { get; set; } = true;
    public bool MappedDrives { get; set; } = true;
    public bool EnvironmentVariables { get; set; }
    public bool ScheduledTasks { get; set; }
    public bool Credentials { get; set; }
    public bool Certificates { get; set; }
    public bool DefaultApps { get; set; } = true;
}

public class ProfileTransportPreferences
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransportMethod PreferredMethod { get; set; } = TransportMethod.NetworkShare;

    public string? NasPath { get; set; }
    public bool Compression { get; set; } = true;
}
