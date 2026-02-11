using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Discovery;

public class CrossPlatformDiscoveryServiceTests
{
    private readonly IPlatformDetectionService _platformDetection = Substitute.For<IPlatformDetectionService>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly CrossPlatformDiscoveryService _service;

    public CrossPlatformDiscoveryServiceTests()
    {
        _service = new CrossPlatformDiscoveryService(
            _platformDetection, _fileSystem, NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task DetectSourcePlatformAsync_DelegatesToPlatformDetection_MacOs()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.MacOs);

        var result = await _service.DetectSourcePlatformAsync(@"E:\");

        result.Should().Be(SourcePlatform.MacOs);
    }

    [Fact]
    public async Task DetectSourcePlatformAsync_DelegatesToPlatformDetection_Linux()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Linux);

        var result = await _service.DetectSourcePlatformAsync(@"E:\");

        result.Should().Be(SourcePlatform.Linux);
    }

    [Fact]
    public async Task DetectSourcePlatformAsync_DelegatesToPlatformDetection_Unknown()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Unknown);

        var result = await _service.DetectSourcePlatformAsync(@"E:\");

        result.Should().Be(SourcePlatform.Unknown);
    }

    [Fact]
    public async Task DiscoverUserProfilesAsync_MacOs_ReturnsProfiles()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.MacOs);
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);

        var profiles = await _service.DiscoverUserProfilesAsync(@"E:\");

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverUserProfilesAsync_Linux_ReturnsProfiles()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Linux);
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);

        var profiles = await _service.DiscoverUserProfilesAsync(@"E:\");

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverApplicationsAsync_MacOs_ReturnsApps()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.MacOs);
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([@"E:\Applications\Safari.app"]);
        _fileSystem.FileExists(@"E:\Applications\Safari.app\Contents\Info.plist").Returns(false);

        var apps = await _service.DiscoverApplicationsAsync(@"E:\");

        apps.Should().ContainSingle(a => a.Name == "Safari");
    }

    [Fact]
    public async Task DiscoverApplicationsAsync_Linux_ReturnsApps()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Linux);
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\vim.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\vim.desktop")
            .Returns("[Desktop Entry]\nName=Vim\nExec=/usr/bin/vim\n");

        var apps = await _service.DiscoverApplicationsAsync(@"E:\");

        apps.Should().ContainSingle(a => a.Name == "Vim");
    }

    [Fact]
    public async Task DiscoverAllAsync_MacOs_ReturnsCombinedResult()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.MacOs);
        _platformDetection.GetOsVersion(@"E:\", SourcePlatform.MacOs).Returns("14.2");

        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([@"E:\Applications\Safari.app"]);
        _fileSystem.FileExists(@"E:\Applications\Safari.app\Contents\Info.plist").Returns(false);

        var result = await _service.DiscoverAllAsync(@"E:\");

        result.Platform.Should().Be(SourcePlatform.MacOs);
        result.OsVersion.Should().Be("14.2");
        result.UserProfiles.Should().NotBeEmpty();
        result.Applications.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiscoverAllAsync_Linux_ReturnsCombinedResult()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Linux);
        _platformDetection.GetOsVersion(@"E:\", SourcePlatform.Linux).Returns("Ubuntu 22.04.3 LTS");

        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);

        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\firefox.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\firefox.desktop")
            .Returns("[Desktop Entry]\nName=Firefox\nExec=/usr/bin/firefox\n");

        var result = await _service.DiscoverAllAsync(@"E:\");

        result.Platform.Should().Be(SourcePlatform.Linux);
        result.OsVersion.Should().Be("Ubuntu 22.04.3 LTS");
        result.UserProfiles.Should().NotBeEmpty();
        result.Applications.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiscoverAllAsync_Unknown_ReturnsEmptyResults()
    {
        _platformDetection.DetectPlatform(@"E:\").Returns(SourcePlatform.Unknown);

        var result = await _service.DiscoverAllAsync(@"E:\");

        result.Platform.Should().Be(SourcePlatform.Unknown);
        result.OsVersion.Should().BeNullOrEmpty();
        result.UserProfiles.Should().BeEmpty();
        result.Applications.Should().BeEmpty();
    }
}
