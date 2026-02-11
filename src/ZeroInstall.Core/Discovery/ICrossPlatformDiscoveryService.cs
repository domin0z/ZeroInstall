using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers user profiles and applications from mounted foreign drives (macOS, Linux).
/// </summary>
public interface ICrossPlatformDiscoveryService
{
    Task<SourcePlatform> DetectSourcePlatformAsync(string sourcePath, CancellationToken ct = default);
    Task<List<UserProfile>> DiscoverUserProfilesAsync(string sourcePath, CancellationToken ct = default);
    Task<List<DiscoveredApplication>> DiscoverApplicationsAsync(string sourcePath, CancellationToken ct = default);
    Task<CrossPlatformDiscoveryResult> DiscoverAllAsync(string sourcePath, CancellationToken ct = default);
}

/// <summary>
/// Combined result of cross-platform discovery: platform info, user profiles, and applications.
/// </summary>
public class CrossPlatformDiscoveryResult
{
    public SourcePlatform Platform { get; set; }
    public string? OsVersion { get; set; }
    public List<UserProfile> UserProfiles { get; set; } = [];
    public List<DiscoveredApplication> Applications { get; set; } = [];
}
