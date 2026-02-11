using FluentAssertions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class PlatformDetectionServiceTests
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly PlatformDetectionService _service;

    public PlatformDetectionServiceTests()
    {
        _service = new PlatformDetectionService(_fileSystem);
    }

    [Fact]
    public void SourcePlatform_HasFourValues()
    {
        Enum.GetValues<SourcePlatform>().Should().HaveCount(4);
    }

    [Fact]
    public void DetectPlatform_MacOsMarkers_ReturnsMacOs()
    {
        _fileSystem.DirectoryExists(@"E:\System\Library\CoreServices").Returns(true);
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);

        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.MacOs);
    }

    [Fact]
    public void DetectPlatform_LinuxWithOsRelease_ReturnsLinux()
    {
        _fileSystem.FileExists(@"E:\etc\os-release").Returns(true);

        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.Linux);
    }

    [Fact]
    public void DetectPlatform_LinuxWithPasswdAndHome_ReturnsLinux()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home").Returns(true);

        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.Linux);
    }

    [Fact]
    public void DetectPlatform_WindowsMarkers_ReturnsWindows()
    {
        _fileSystem.DirectoryExists(@"E:\Windows\System32").Returns(true);
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);

        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.Windows);
    }

    [Fact]
    public void DetectPlatform_EmptyDirectory_ReturnsUnknown()
    {
        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.Unknown);
    }

    [Fact]
    public void DetectPlatform_MixedMarkers_MacOsTakesPriority()
    {
        // macOS markers + Linux markers — macOS checked first
        _fileSystem.DirectoryExists(@"E:\System\Library\CoreServices").Returns(true);
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.FileExists(@"E:\etc\os-release").Returns(true);

        _service.DetectPlatform(@"E:\").Should().Be(SourcePlatform.MacOs);
    }

    [Fact]
    public void ParseMacOsVersion_ValidPlist_ReturnsVersion()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>ProductBuildVersion</key>
                <string>23B92</string>
                <key>ProductName</key>
                <string>macOS</string>
                <key>ProductVersion</key>
                <string>14.2</string>
            </dict>
            </plist>
            """;

        PlatformDetectionService.ParseMacOsVersion(plist).Should().Be("14.2");
    }

    [Fact]
    public void ParseMacOsVersion_MissingProductVersion_ReturnsNull()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>ProductName</key>
                <string>macOS</string>
            </dict>
            </plist>
            """;

        PlatformDetectionService.ParseMacOsVersion(plist).Should().BeNull();
    }

    [Fact]
    public void ParseLinuxOsRelease_StandardContent_ReturnsPrettyName()
    {
        var content = """
            NAME="Ubuntu"
            VERSION="22.04.3 LTS (Jammy Jellyfish)"
            ID=ubuntu
            PRETTY_NAME="Ubuntu 22.04.3 LTS"
            VERSION_ID="22.04"
            """;

        PlatformDetectionService.ParseLinuxOsRelease(content).Should().Be("Ubuntu 22.04.3 LTS");
    }

    [Fact]
    public void ParseLinuxOsRelease_MissingPrettyName_ReturnsNull()
    {
        var content = """
            NAME="Arch Linux"
            ID=arch
            """;

        PlatformDetectionService.ParseLinuxOsRelease(content).Should().BeNull();
    }

    [Fact]
    public void GetOsVersion_MacOs_ParsesSystemVersionPlist()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>ProductVersion</key>
                <string>14.2</string>
            </dict>
            </plist>
            """;

        _fileSystem.FileExists(@"E:\System\Library\CoreServices\SystemVersion.plist").Returns(true);
        _fileSystem.ReadAllText(@"E:\System\Library\CoreServices\SystemVersion.plist").Returns(plist);

        _service.GetOsVersion(@"E:\", SourcePlatform.MacOs).Should().Be("14.2");
    }

    [Fact]
    public void GetOsVersion_Linux_ParsesOsRelease()
    {
        var content = "PRETTY_NAME=\"Fedora Linux 39\"";

        _fileSystem.FileExists(@"E:\etc\os-release").Returns(true);
        _fileSystem.ReadAllText(@"E:\etc\os-release").Returns(content);

        _service.GetOsVersion(@"E:\", SourcePlatform.Linux).Should().Be("Fedora Linux 39");
    }
}
