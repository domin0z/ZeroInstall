using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Models;

/// <summary>
/// Application-level preferences persisted as JSON.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// UNC path to a NAS share for shared profiles and reports.
    /// </summary>
    public string? NasPath { get; set; }

    /// <summary>
    /// Default transport method for new migrations.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransportMethod DefaultTransportMethod { get; set; } = TransportMethod.ExternalStorage;

    /// <summary>
    /// Minimum log level (Information, Warning, Error, etc.).
    /// </summary>
    public string DefaultLogLevel { get; set; } = "Information";
}
