using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// An application found on the source machine during discovery.
/// </summary>
public class DiscoveredApplication
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string? InstallLocation { get; set; }
    public string? UninstallString { get; set; }

    /// <summary>
    /// Registry path where this app's uninstall entry was found.
    /// </summary>
    public string RegistryKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a 32-bit app installed under WOW6432Node.
    /// </summary>
    public bool Is32Bit { get; set; }

    /// <summary>
    /// Whether this is a per-user install (HKCU) rather than machine-wide (HKLM).
    /// </summary>
    public bool IsPerUser { get; set; }

    /// <summary>
    /// Winget package ID if matched, null otherwise.
    /// </summary>
    public string? WingetPackageId { get; set; }

    /// <summary>
    /// Chocolatey package ID if matched, null otherwise.
    /// </summary>
    public string? ChocolateyPackageId { get; set; }

    /// <summary>
    /// Homebrew cask ID if matched (macOS), null otherwise.
    /// </summary>
    public string? BrewCaskId { get; set; }

    /// <summary>
    /// APT package name if matched (Linux Debian/Ubuntu), null otherwise.
    /// </summary>
    public string? AptPackageName { get; set; }

    /// <summary>
    /// Snap package name if matched (Linux), null otherwise.
    /// </summary>
    public string? SnapPackageName { get; set; }

    /// <summary>
    /// Flatpak application ID if matched (Linux), null otherwise.
    /// </summary>
    public string? FlatpakAppId { get; set; }

    /// <summary>
    /// Estimated total size in bytes (Program Files + AppData).
    /// </summary>
    public long EstimatedSizeBytes { get; set; }

    /// <summary>
    /// The recommended migration tier based on package manager availability.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MigrationTier RecommendedTier =>
        WingetPackageId is not null || ChocolateyPackageId is not null
            ? MigrationTier.Package
            : MigrationTier.RegistryFile;

    /// <summary>
    /// AppData paths associated with this application.
    /// </summary>
    public List<string> AppDataPaths { get; set; } = [];

    /// <summary>
    /// Additional registry key paths to capture for Tier 2 migration.
    /// </summary>
    public List<string> AdditionalRegistryPaths { get; set; } = [];
}
