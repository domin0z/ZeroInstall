using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Tests.Discovery;

public class MacOsUserProfileDiscoveryServiceTests
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly MacOsUserProfileDiscoveryService _service;

    public MacOsUserProfileDiscoveryServiceTests()
    {
        _service = new MacOsUserProfileDiscoveryService(
            _fileSystem, NullLogger<MacOsUserProfileDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAsync_FindsUsersFromUsersDirectory()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john", @"E:\Users\jane"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles.Should().HaveCount(2);
        profiles[0].Username.Should().Be("john");
        profiles[1].Username.Should().Be("jane");
    }

    [Fact]
    public async Task DiscoverAsync_SetsCorrectProperties()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        var profile = profiles.Should().ContainSingle().Subject;
        profile.Sid.Should().Be("john");
        profile.ProfilePath.Should().Be(@"E:\Users\john");
        profile.IsLocal.Should().BeTrue();
        profile.AccountType.Should().Be(UserAccountType.Local);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsSharedDirectory()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\Shared", @"E:\Users\john"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverAsync_SkipsGuestDirectory()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\Guest", @"E:\Users\john"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverAsync_MapsDocumentsFolder()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Documents").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].Folders.Documents.Should().Be(@"E:\Users\john\Documents");
    }

    [Fact]
    public async Task DiscoverAsync_MapsMacOsMoviesToVideos()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Movies").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].Folders.Videos.Should().Be(@"E:\Users\john\Movies");
    }

    [Fact]
    public async Task DiscoverAsync_MapsLibraryAppSupportToAppDataRoaming()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Application Support").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].Folders.AppDataRoaming.Should().Be(@"E:\Users\john\Library\Application Support");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversChromeProfile()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Application Support\Google\Chrome").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Chrome");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversFirefoxProfiles()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Application Support\Firefox\Profiles").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users\john\Library\Application Support\Firefox\Profiles")
            .Returns([@"E:\Users\john\Library\Application Support\Firefox\Profiles\abc123.default"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Firefox");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversSafariProfile()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Safari").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Safari");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversEdgeProfile()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Application Support\Microsoft Edge").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Edge");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversOutlookEmail()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Group Containers\UBF8T346G9.Office\Outlook\Outlook 15 Profiles").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].EmailData.Should().ContainSingle(e => e.ClientName == "Outlook");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversThunderbirdEmail()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Thunderbird\Profiles").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].EmailData.Should().ContainSingle(e => e.ClientName == "Thunderbird");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversAppleMailEmail()
    {
        _fileSystem.DirectoryExists(@"E:\Users").Returns(true);
        _fileSystem.GetDirectories(@"E:\Users").Returns([@"E:\Users\john"]);
        _fileSystem.DirectoryExists(@"E:\Users\john\Library\Mail").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].EmailData.Should().ContainSingle(e => e.ClientName == "Apple Mail");
    }
}

public class MacOsApplicationDiscoveryServiceTests
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly MacOsApplicationDiscoveryService _service;

    public MacOsApplicationDiscoveryServiceTests()
    {
        _service = new MacOsApplicationDiscoveryService(
            _fileSystem, NullLogger<MacOsApplicationDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAsync_FindsAppBundles()
    {
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([@"E:\Applications\Chrome.app"]);
        _fileSystem.FileExists(@"E:\Applications\Chrome.app\Contents\Info.plist").Returns(false);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle();
        apps[0].Name.Should().Be("Chrome");
        apps[0].InstallLocation.Should().Be(@"E:\Applications\Chrome.app");
    }

    [Fact]
    public async Task DiscoverAsync_ParsesInfoPlistForAppBundle()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>CFBundleName</key>
                <string>Google Chrome</string>
                <key>CFBundleShortVersionString</key>
                <string>120.0.6099.129</string>
                <key>CFBundleIdentifier</key>
                <string>com.google.Chrome</string>
            </dict>
            </plist>
            """;

        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([@"E:\Applications\Google Chrome.app"]);
        _fileSystem.FileExists(@"E:\Applications\Google Chrome.app\Contents\Info.plist").Returns(true);
        _fileSystem.ReadAllText(@"E:\Applications\Google Chrome.app\Contents\Info.plist").Returns(plist);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps[0].Name.Should().Be("Google Chrome");
        apps[0].Version.Should().Be("120.0.6099.129");
        apps[0].Publisher.Should().Be("Google");
    }

    [Fact]
    public void ParseInfoPlist_ExtractsCFBundleName()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>CFBundleName</key>
                <string>Safari</string>
            </dict>
            </plist>
            """;

        var (name, _, _) = MacOsApplicationDiscoveryService.ParseInfoPlist(plist);
        name.Should().Be("Safari");
    }

    [Fact]
    public void ParseInfoPlist_ExtractsVersion()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>CFBundleShortVersionString</key>
                <string>17.2</string>
            </dict>
            </plist>
            """;

        var (_, version, _) = MacOsApplicationDiscoveryService.ParseInfoPlist(plist);
        version.Should().Be("17.2");
    }

    [Fact]
    public void ParseInfoPlist_ExtractsBundleIdentifier()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>CFBundleIdentifier</key>
                <string>com.apple.Safari</string>
            </dict>
            </plist>
            """;

        var (_, _, bundleId) = MacOsApplicationDiscoveryService.ParseInfoPlist(plist);
        bundleId.Should().Be("com.apple.Safari");
    }

    [Fact]
    public void ParseInfoPlist_MissingKeys_ReturnsNulls()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
            </dict>
            </plist>
            """;

        var (name, version, bundleId) = MacOsApplicationDiscoveryService.ParseInfoPlist(plist);
        name.Should().BeNull();
        version.Should().BeNull();
        bundleId.Should().BeNull();
    }

    [Fact]
    public void ParseInfoPlist_MalformedXml_ReturnsNulls()
    {
        var (name, version, bundleId) = MacOsApplicationDiscoveryService.ParseInfoPlist("not xml");
        name.Should().BeNull();
        version.Should().BeNull();
        bundleId.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversHomebrewCellarInstalls()
    {
        _fileSystem.DirectoryExists(@"E:\usr\local\Cellar").Returns(true);
        _fileSystem.GetDirectories(@"E:\usr\local\Cellar").Returns([@"E:\usr\local\Cellar\wget"]);
        _fileSystem.GetDirectories(@"E:\usr\local\Cellar\wget").Returns([@"E:\usr\local\Cellar\wget\1.21.4"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.Name == "wget");
        apps[0].Version.Should().Be("1.21.4");
        apps[0].Publisher.Should().Be("Homebrew");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversHomebrewCellarUsesLatestVersion()
    {
        _fileSystem.DirectoryExists(@"E:\usr\local\Cellar").Returns(true);
        _fileSystem.GetDirectories(@"E:\usr\local\Cellar").Returns([@"E:\usr\local\Cellar\git"]);
        _fileSystem.GetDirectories(@"E:\usr\local\Cellar\git")
            .Returns([@"E:\usr\local\Cellar\git\2.42.0", @"E:\usr\local\Cellar\git\2.43.0"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.Name == "git");
        apps[0].Version.Should().Be("2.43.0");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversCaskroomAndSetsBrewCaskId()
    {
        _fileSystem.DirectoryExists(@"E:\usr\local\Caskroom").Returns(true);
        _fileSystem.GetDirectories(@"E:\usr\local\Caskroom").Returns([@"E:\usr\local\Caskroom\visual-studio-code"]);
        _fileSystem.GetDirectories(@"E:\usr\local\Caskroom\visual-studio-code")
            .Returns([@"E:\usr\local\Caskroom\visual-studio-code\1.85.0"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.BrewCaskId == "visual-studio-code");
        apps[0].Version.Should().Be("1.85.0");
    }

    [Fact]
    public async Task DiscoverAsync_TagsExistingAppWithBrewCaskId()
    {
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([@"E:\Applications\Firefox.app"]);
        _fileSystem.FileExists(@"E:\Applications\Firefox.app\Contents\Info.plist").Returns(false);

        _fileSystem.DirectoryExists(@"E:\usr\local\Caskroom").Returns(true);
        _fileSystem.GetDirectories(@"E:\usr\local\Caskroom").Returns([@"E:\usr\local\Caskroom\firefox"]);
        _fileSystem.GetDirectories(@"E:\usr\local\Caskroom\firefox")
            .Returns([@"E:\usr\local\Caskroom\firefox\121.0"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle();
        apps[0].Name.Should().Be("Firefox");
        apps[0].BrewCaskId.Should().Be("firefox");
    }

    [Fact]
    public async Task DiscoverAsync_HandlesAppleSiliconHomebrewPath()
    {
        _fileSystem.DirectoryExists(@"E:\opt\homebrew\Cellar").Returns(true);
        _fileSystem.GetDirectories(@"E:\opt\homebrew\Cellar").Returns([@"E:\opt\homebrew\Cellar\node"]);
        _fileSystem.GetDirectories(@"E:\opt\homebrew\Cellar\node").Returns([@"E:\opt\homebrew\Cellar\node\21.5.0"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.Name == "node");
    }

    [Fact]
    public async Task DiscoverAsync_EmptyApplicationsDirectory_ReturnsEmptyList()
    {
        _fileSystem.DirectoryExists(@"E:\Applications").Returns(true);
        _fileSystem.GetDirectories(@"E:\Applications").Returns([]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPublisher_ValidBundleId_ReturnsCapitalizedSecondComponent()
    {
        MacOsApplicationDiscoveryService.ExtractPublisher("com.google.Chrome").Should().Be("Google");
    }

    [Fact]
    public void ExtractPublisher_NullBundleId_ReturnsNull()
    {
        MacOsApplicationDiscoveryService.ExtractPublisher(null).Should().BeNull();
    }
}
