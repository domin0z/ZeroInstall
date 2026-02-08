using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Core.Tests.Discovery;

public class UserProfileDiscoveryTests
{
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly UserProfileDiscoveryService _service;

    public UserProfileDiscoveryTests()
    {
        _service = new UserProfileDiscoveryService(
            _registry, _fileSystem,
            NullLogger<UserProfileDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAsync_FindsUserProfiles()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList")
            .Returns(new[] { "S-1-5-18", "S-1-5-21-123-456-789-1001", "S-1-5-21-123-456-789-1002" });

        SetupProfile("S-1-5-21-123-456-789-1001", @"C:\Users\Bill");
        SetupProfile("S-1-5-21-123-456-789-1002", @"C:\Users\Admin");

        var profiles = await _service.DiscoverAsync();

        profiles.Should().HaveCount(2);
        profiles[0].Username.Should().Be("Bill");
        profiles[0].Sid.Should().Be("S-1-5-21-123-456-789-1001");
        profiles[1].Username.Should().Be("Admin");
    }

    [Fact]
    public async Task DiscoverAsync_SkipsSystemSids()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList")
            .Returns(new[] { "S-1-5-18", "S-1-5-19", "S-1-5-20", "S-1-5-21-123-456-789-1001" });

        SetupProfile("S-1-5-21-123-456-789-1001", @"C:\Users\RealUser");

        var profiles = await _service.DiscoverAsync();

        profiles.Should().HaveCount(1);
        profiles[0].Username.Should().Be("RealUser");
    }

    [Fact]
    public void DiscoverFolders_MapsKnownFolders()
    {
        var profilePath = @"C:\Users\TestUser";
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.StartsWith(profilePath))).Returns(true);

        var folders = _service.DiscoverFolders(profilePath);

        folders.Documents.Should().Be(@"C:\Users\TestUser\Documents");
        folders.Desktop.Should().Be(@"C:\Users\TestUser\Desktop");
        folders.Downloads.Should().Be(@"C:\Users\TestUser\Downloads");
        folders.Pictures.Should().Be(@"C:\Users\TestUser\Pictures");
        folders.Music.Should().Be(@"C:\Users\TestUser\Music");
        folders.Videos.Should().Be(@"C:\Users\TestUser\Videos");
        folders.AppDataRoaming.Should().Be(@"C:\Users\TestUser\AppData\Roaming");
        folders.AppDataLocal.Should().Be(@"C:\Users\TestUser\AppData\Local");
    }

    [Fact]
    public void DiscoverFolders_ReturnsNullForMissingFolders()
    {
        var profilePath = @"C:\Users\TestUser";
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var folders = _service.DiscoverFolders(profilePath);

        folders.Documents.Should().BeNull();
        folders.Desktop.Should().BeNull();
    }

    [Fact]
    public void DiscoverBrowserProfiles_FindsChrome()
    {
        var profilePath = @"C:\Users\TestUser";
        var chromeBase = @"C:\Users\TestUser\AppData\Local\Google\Chrome\User Data";
        var chromeDefault = Path.Combine(chromeBase, "Default");

        _fileSystem.DirectoryExists(chromeBase).Returns(true);
        _fileSystem.DirectoryExists(chromeDefault).Returns(true);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Firefox"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Edge"))).Returns(false);
        _fileSystem.GetDirectories(chromeBase).Returns(new[] { chromeDefault });
        _fileSystem.GetDirectorySize(chromeDefault).Returns(500_000_000L);

        var browsers = _service.DiscoverBrowserProfiles(profilePath);

        browsers.Should().HaveCount(1);
        browsers[0].BrowserName.Should().Be("Google Chrome");
        browsers[0].ProfileName.Should().Be("Default");
        browsers[0].EstimatedSizeBytes.Should().Be(500_000_000L);
    }

    [Fact]
    public void DiscoverEmailData_FindsOutlookPstFiles()
    {
        var profilePath = @"C:\Users\TestUser";
        var outlookDir = @"C:\Users\TestUser\Documents\Outlook Files";

        _fileSystem.DirectoryExists(outlookDir).Returns(true);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Microsoft\\Outlook"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Thunderbird"))).Returns(false);
        _fileSystem.GetFiles(outlookDir, "*.pst").Returns(new[] { @"C:\Users\TestUser\Documents\Outlook Files\archive.pst" });
        _fileSystem.GetFiles(outlookDir, "*.ost").Returns(Array.Empty<string>());
        _fileSystem.GetFileSize(Arg.Any<string>()).Returns(2_000_000_000L);

        var email = _service.DiscoverEmailData(profilePath);

        email.Should().HaveCount(1);
        email[0].ClientName.Should().Be("Microsoft Outlook");
        email[0].DataPaths.Should().HaveCount(1);
        email[0].EstimatedSizeBytes.Should().Be(2_000_000_000L);
    }

    private void SetupProfile(string sid, string profilePath)
    {
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}",
            "ProfileImagePath")
            .Returns(profilePath);

        _fileSystem.DirectoryExists(profilePath).Returns(true);
        _fileSystem.DirectoryExists(Arg.Is<string>(s =>
            s.StartsWith(profilePath) && !s.Contains("Google") && !s.Contains("Firefox")
            && !s.Contains("Edge") && !s.Contains("Outlook") && !s.Contains("Thunderbird")))
            .Returns(true);

        // Return empty for browser/email paths by default
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Google"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Firefox"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Edge"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Outlook"))).Returns(false);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("Thunderbird"))).Returns(false);
        _fileSystem.GetDirectorySize(Arg.Any<string>()).Returns(0L);
    }
}
