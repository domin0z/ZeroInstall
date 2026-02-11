using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Orchestrates cross-platform discovery by detecting the source platform and delegating
/// to the correct macOS or Linux discovery services.
/// </summary>
internal class CrossPlatformDiscoveryService : ICrossPlatformDiscoveryService
{
    private readonly IPlatformDetectionService _platformDetection;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILoggerFactory _loggerFactory;

    public CrossPlatformDiscoveryService(
        IPlatformDetectionService platformDetection,
        IFileSystemAccessor fileSystem,
        ILoggerFactory loggerFactory)
    {
        _platformDetection = platformDetection;
        _fileSystem = fileSystem;
        _loggerFactory = loggerFactory;
    }

    public Task<SourcePlatform> DetectSourcePlatformAsync(string sourcePath, CancellationToken ct = default)
    {
        var platform = _platformDetection.DetectPlatform(sourcePath);
        return Task.FromResult(platform);
    }

    public async Task<List<UserProfile>> DiscoverUserProfilesAsync(string sourcePath, CancellationToken ct = default)
    {
        var platform = _platformDetection.DetectPlatform(sourcePath);

        return platform switch
        {
            SourcePlatform.MacOs => await CreateMacOsUserDiscovery().DiscoverAsync(sourcePath, ct),
            SourcePlatform.Linux => await CreateLinuxUserDiscovery().DiscoverAsync(sourcePath, ct),
            _ => []
        };
    }

    public async Task<List<DiscoveredApplication>> DiscoverApplicationsAsync(string sourcePath, CancellationToken ct = default)
    {
        var platform = _platformDetection.DetectPlatform(sourcePath);

        return platform switch
        {
            SourcePlatform.MacOs => await CreateMacOsAppDiscovery().DiscoverAsync(sourcePath, ct),
            SourcePlatform.Linux => await CreateLinuxAppDiscovery().DiscoverAsync(sourcePath, ct),
            _ => []
        };
    }

    public async Task<CrossPlatformDiscoveryResult> DiscoverAllAsync(string sourcePath, CancellationToken ct = default)
    {
        var platform = _platformDetection.DetectPlatform(sourcePath);
        var osVersion = _platformDetection.GetOsVersion(sourcePath, platform);

        var result = new CrossPlatformDiscoveryResult
        {
            Platform = platform,
            OsVersion = osVersion
        };

        switch (platform)
        {
            case SourcePlatform.MacOs:
                result.UserProfiles = await CreateMacOsUserDiscovery().DiscoverAsync(sourcePath, ct);
                result.Applications = await CreateMacOsAppDiscovery().DiscoverAsync(sourcePath, ct);
                break;

            case SourcePlatform.Linux:
                result.UserProfiles = await CreateLinuxUserDiscovery().DiscoverAsync(sourcePath, ct);
                result.Applications = await CreateLinuxAppDiscovery().DiscoverAsync(sourcePath, ct);
                break;
        }

        return result;
    }

    private MacOsUserProfileDiscoveryService CreateMacOsUserDiscovery() =>
        new(_fileSystem, _loggerFactory.CreateLogger<MacOsUserProfileDiscoveryService>());

    private MacOsApplicationDiscoveryService CreateMacOsAppDiscovery() =>
        new(_fileSystem, _loggerFactory.CreateLogger<MacOsApplicationDiscoveryService>());

    private LinuxUserProfileDiscoveryService CreateLinuxUserDiscovery() =>
        new(_fileSystem, _loggerFactory.CreateLogger<LinuxUserProfileDiscoveryService>());

    private LinuxApplicationDiscoveryService CreateLinuxAppDiscovery() =>
        new(_fileSystem, _loggerFactory.CreateLogger<LinuxApplicationDiscoveryService>());
}
