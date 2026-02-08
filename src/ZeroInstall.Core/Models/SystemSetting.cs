using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// A transferable system-level setting discovered on the source machine.
/// </summary>
public class SystemSetting
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SystemSettingCategory Category { get; set; }

    /// <summary>
    /// Opaque data payload for this setting (serialized for transport).
    /// The format depends on the category.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// For file-backed settings, the source file path(s).
    /// </summary>
    public List<string> SourcePaths { get; set; } = [];
}

/// <summary>
/// Categories of system settings that can be transferred.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SystemSettingCategory
{
    WifiProfile,
    Printer,
    MappedDrive,
    EnvironmentVariable,
    ScheduledTask,
    Credential,
    Certificate,
    DefaultAppAssociation
}
