using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Tests.Discovery;

public class LinuxUserProfileDiscoveryServiceTests
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly LinuxUserProfileDiscoveryService _service;

    public LinuxUserProfileDiscoveryServiceTests()
    {
        _service = new LinuxUserProfileDiscoveryService(
            _fileSystem, NullLogger<LinuxUserProfileDiscoveryService>.Instance);
    }

    [Fact]
    public void ParseEtcPasswd_ValidEntries_ReturnsUsers()
    {
        var lines = new[]
        {
            "john:x:1000:1000:John Doe:/home/john:/bin/bash",
            "jane:x:1001:1001:Jane Doe:/home/jane:/bin/zsh"
        };

        var entries = LinuxUserProfileDiscoveryService.ParseEtcPasswd(lines);

        entries.Should().HaveCount(2);
        entries[0].Username.Should().Be("john");
        entries[0].Uid.Should().Be(1000);
        entries[0].HomeDir.Should().Be("/home/john");
        entries[1].Username.Should().Be("jane");
    }

    [Fact]
    public void ParseEtcPasswd_FiltersSystemUsers()
    {
        var lines = new[]
        {
            "root:x:0:0:root:/root:/bin/bash",
            "daemon:x:1:1:daemon:/usr/sbin:/usr/sbin/nologin",
            "www-data:x:33:33:www-data:/var/www:/usr/sbin/nologin",
            "john:x:1000:1000::/home/john:/bin/bash"
        };

        var entries = LinuxUserProfileDiscoveryService.ParseEtcPasswd(lines);

        entries.Should().ContainSingle();
        entries[0].Username.Should().Be("john");
    }

    [Fact]
    public void ParseEtcPasswd_FiltersNologinShells()
    {
        var lines = new[]
        {
            "sshd:x:1001:1001::/var/run/sshd:/usr/sbin/nologin",
            "nobody:x:1002:1002::/nonexistent:/bin/false",
            "john:x:1000:1000::/home/john:/bin/bash"
        };

        var entries = LinuxUserProfileDiscoveryService.ParseEtcPasswd(lines);

        entries.Should().ContainSingle();
        entries[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverAsync_FindsUsersFromPasswd()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000:John Doe:/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
        profiles[0].Sid.Should().Be("1000");
        profiles[0].ProfilePath.Should().Be(@"E:\home\john");
        profiles[0].IsLocal.Should().BeTrue();
        profiles[0].AccountType.Should().Be(UserAccountType.Local);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsUsersWithoutHomeOnDisk()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash",
            "ghost:x:1001:1001::/home/ghost:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\ghost").Returns(false);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles.Should().ContainSingle();
        profiles[0].Username.Should().Be("john");
    }

    [Fact]
    public async Task DiscoverAsync_MapsXdgFolders()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\Documents").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\Desktop").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.config").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.local\share").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].Folders.Documents.Should().Be(@"E:\home\john\Documents");
        profiles[0].Folders.Desktop.Should().Be(@"E:\home\john\Desktop");
        profiles[0].Folders.AppDataRoaming.Should().Be(@"E:\home\john\.config");
        profiles[0].Folders.AppDataLocal.Should().Be(@"E:\home\john\.local\share");
    }

    [Fact]
    public async Task DiscoverAsync_MapsVideosFolderDirectly()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\Videos").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].Folders.Videos.Should().Be(@"E:\home\john\Videos");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversChromeProfile()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.config\google-chrome").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Chrome");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversFirefoxProfiles()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.mozilla\firefox").Returns(true);
        _fileSystem.GetDirectories(@"E:\home\john\.mozilla\firefox")
            .Returns([@"E:\home\john\.mozilla\firefox\abc123.default-release"]);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Firefox");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversChromiumProfile()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.config\chromium").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].BrowserProfiles.Should().ContainSingle(b => b.BrowserName == "Chromium");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversThunderbirdEmail()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.thunderbird").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].EmailData.Should().ContainSingle(e => e.ClientName == "Thunderbird");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversEvolutionEmail()
    {
        _fileSystem.FileExists(@"E:\etc\passwd").Returns(true);
        _fileSystem.ReadAllLines(@"E:\etc\passwd").Returns([
            "john:x:1000:1000::/home/john:/bin/bash"
        ]);
        _fileSystem.DirectoryExists(@"E:\home\john").Returns(true);
        _fileSystem.DirectoryExists(@"E:\home\john\.local\share\evolution\mail").Returns(true);

        var profiles = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        profiles[0].EmailData.Should().ContainSingle(e => e.ClientName == "Evolution");
    }
}

public class LinuxApplicationDiscoveryServiceTests
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly LinuxApplicationDiscoveryService _service;

    public LinuxApplicationDiscoveryServiceTests()
    {
        _service = new LinuxApplicationDiscoveryService(
            _fileSystem, NullLogger<LinuxApplicationDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAsync_FindsDesktopFiles()
    {
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\firefox.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\firefox.desktop")
            .Returns("[Desktop Entry]\nName=Firefox\nExec=/usr/bin/firefox\n");

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.Name == "Firefox");
    }

    [Fact]
    public async Task DiscoverAsync_ParsesDesktopFileWithVersion()
    {
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\code.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\code.desktop")
            .Returns("[Desktop Entry]\nName=Visual Studio Code\nVersion=1.85.0\nExec=/usr/bin/code\nComment=Code editor\n");

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps[0].Name.Should().Be("Visual Studio Code");
        apps[0].Version.Should().Be("1.85.0");
        apps[0].Publisher.Should().Be("Code editor");
    }

    [Fact]
    public void ParseDesktopFile_ExtractsAllFields()
    {
        var content = """
            [Desktop Entry]
            Name=Firefox Web Browser
            Version=1.0
            Exec=/usr/bin/firefox %u
            Comment=Browse the World Wide Web
            Type=Application
            """;

        var entry = LinuxApplicationDiscoveryService.ParseDesktopFile(content);

        entry.Name.Should().Be("Firefox Web Browser");
        entry.Version.Should().Be("1.0");
        entry.Exec.Should().Be("/usr/bin/firefox %u");
        entry.Comment.Should().Be("Browse the World Wide Web");
    }

    [Fact]
    public void ParseDesktopFile_MissingFields_ReturnsNulls()
    {
        var content = """
            [Desktop Entry]
            Type=Application
            """;

        var entry = LinuxApplicationDiscoveryService.ParseDesktopFile(content);

        entry.Name.Should().BeNull();
        entry.Version.Should().BeNull();
    }

    [Fact]
    public void ParseDpkgStatus_ParsesInstalledPackages()
    {
        var content = """
            Package: firefox
            Status: install ok installed
            Version: 121.0-1
            Description: Mozilla Firefox web browser

            Package: vim
            Status: install ok installed
            Version: 9.0.1000-4
            Description: Vi IMproved - enhanced vi editor

            """;

        var packages = LinuxApplicationDiscoveryService.ParseDpkgStatus(content);

        packages.Should().HaveCount(2);
        packages[0].PackageName.Should().Be("firefox");
        packages[0].Version.Should().Be("121.0-1");
        packages[1].PackageName.Should().Be("vim");
    }

    [Fact]
    public void ParseDpkgStatus_SkipsUninstalledPackages()
    {
        var content = """
            Package: removed-pkg
            Status: deinstall ok config-files
            Version: 1.0

            Package: firefox
            Status: install ok installed
            Version: 121.0

            """;

        var packages = LinuxApplicationDiscoveryService.ParseDpkgStatus(content);

        packages.Should().ContainSingle();
        packages[0].PackageName.Should().Be("firefox");
    }

    [Fact]
    public async Task DiscoverAsync_MatchesDesktopAppToDpkgPackage()
    {
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\firefox.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\firefox.desktop")
            .Returns("[Desktop Entry]\nName=Firefox\nExec=/usr/bin/firefox\n");

        _fileSystem.FileExists(@"E:\var\lib\dpkg\status").Returns(true);
        _fileSystem.ReadAllText(@"E:\var\lib\dpkg\status")
            .Returns("Package: firefox\nStatus: install ok installed\nVersion: 121.0\n\n");

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.Name == "Firefox" && a.AptPackageName == "firefox");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversSnapInstalls()
    {
        _fileSystem.DirectoryExists(@"E:\snap").Returns(true);
        _fileSystem.GetDirectories(@"E:\snap").Returns([@"E:\snap\spotify", @"E:\snap\bin"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.SnapPackageName == "spotify");
        apps[0].Name.Should().Be("spotify");
    }

    [Fact]
    public async Task DiscoverAsync_TagsExistingAppWithSnapPackageName()
    {
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\spotify.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\spotify.desktop")
            .Returns("[Desktop Entry]\nName=Spotify\nExec=/snap/bin/spotify\n");

        _fileSystem.DirectoryExists(@"E:\snap").Returns(true);
        _fileSystem.GetDirectories(@"E:\snap").Returns([@"E:\snap\spotify"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        var spotifyApps = apps.Where(a =>
            a.Name.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals("spotify", StringComparison.OrdinalIgnoreCase)).ToList();
        spotifyApps.Should().ContainSingle();
        spotifyApps[0].SnapPackageName.Should().Be("spotify");
    }

    [Fact]
    public async Task DiscoverAsync_DiscoversFlatpakInstalls()
    {
        _fileSystem.DirectoryExists(@"E:\var\lib\flatpak\app").Returns(true);
        _fileSystem.GetDirectories(@"E:\var\lib\flatpak\app")
            .Returns([@"E:\var\lib\flatpak\app\org.mozilla.firefox"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        apps.Should().ContainSingle(a => a.FlatpakAppId == "org.mozilla.firefox");
        apps[0].Name.Should().Be("firefox");
    }

    [Fact]
    public async Task DiscoverAsync_TagsExistingAppWithFlatpakAppId()
    {
        _fileSystem.DirectoryExists(@"E:\usr\share\applications").Returns(true);
        _fileSystem.GetFiles(@"E:\usr\share\applications", "*.desktop")
            .Returns([@"E:\usr\share\applications\firefox.desktop"]);
        _fileSystem.ReadAllText(@"E:\usr\share\applications\firefox.desktop")
            .Returns("[Desktop Entry]\nName=firefox\nExec=/usr/bin/firefox\n");

        _fileSystem.DirectoryExists(@"E:\var\lib\flatpak\app").Returns(true);
        _fileSystem.GetDirectories(@"E:\var\lib\flatpak\app")
            .Returns([@"E:\var\lib\flatpak\app\org.mozilla.firefox"]);

        var apps = await _service.DiscoverAsync(@"E:\", CancellationToken.None);

        var firefoxApps = apps.Where(a =>
            a.Name.Equals("firefox", StringComparison.OrdinalIgnoreCase)).ToList();
        firefoxApps.Should().ContainSingle();
        firefoxApps[0].FlatpakAppId.Should().Be("org.mozilla.firefox");
    }

    [Fact]
    public void ParseDpkgStatus_MalformedEntry_IsSkipped()
    {
        var content = "This is not a valid dpkg status file\n\n";

        var packages = LinuxApplicationDiscoveryService.ParseDpkgStatus(content);

        packages.Should().BeEmpty();
    }
}
