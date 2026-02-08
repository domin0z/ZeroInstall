using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class BrowserDataServiceTests : IDisposable
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly BrowserDataService _service;
    private readonly string _tempDir;

    public BrowserDataServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-browser-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new BrowserDataService(
            _fileSystem, _processRunner, NullLogger<BrowserDataService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UserProfile CreateProfileWithBrowser(string username, string browserName,
        string profileName, string profilePath)
    {
        return new UserProfile
        {
            Username = username,
            ProfilePath = $@"C:\Users\{username}",
            BrowserProfiles =
            [
                new BrowserProfile
                {
                    BrowserName = browserName,
                    ProfileName = profileName,
                    ProfilePath = profilePath
                }
            ]
        };
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesManifest()
    {
        var browserPath = Path.Combine(_tempDir, "chrome-source");
        Directory.CreateDirectory(browserPath);
        File.WriteAllText(Path.Combine(browserPath, "Bookmarks"), "{}");

        _fileSystem.DirectoryExists(browserPath).Returns(true);

        var profile = CreateProfileWithBrowser("Alice", "Chrome", "Default", browserPath);
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice - Chrome",
                ItemType = MigrationItemType.BrowserData,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "browser-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CopiesBrowserFiles()
    {
        var browserPath = Path.Combine(_tempDir, "chrome-data");
        Directory.CreateDirectory(browserPath);
        File.WriteAllText(Path.Combine(browserPath, "Bookmarks"), "{\"bookmarks\":[]}");
        File.WriteAllText(Path.Combine(browserPath, "Preferences"), "{\"prefs\":{}}");

        _fileSystem.DirectoryExists(browserPath).Returns(true);

        var profile = CreateProfileWithBrowser("Alice", "Chrome", "Default", browserPath);
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice - Chrome",
                ItemType = MigrationItemType.BrowserData,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture-files");
        await _service.CaptureAsync(items, outputDir);

        var capturedDir = Path.Combine(outputDir, "Alice_Chrome_Default");
        File.Exists(Path.Combine(capturedDir, "Bookmarks")).Should().BeTrue();
        File.Exists(Path.Combine(capturedDir, "Preferences")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_ExcludesCacheDirectories()
    {
        var browserPath = Path.Combine(_tempDir, "chrome-cache");
        Directory.CreateDirectory(browserPath);
        Directory.CreateDirectory(Path.Combine(browserPath, "Cache"));
        Directory.CreateDirectory(Path.Combine(browserPath, "Code Cache"));
        Directory.CreateDirectory(Path.Combine(browserPath, "GPUCache"));
        Directory.CreateDirectory(Path.Combine(browserPath, "Extensions"));
        File.WriteAllText(Path.Combine(browserPath, "Cache", "data.tmp"), "cached");
        File.WriteAllText(Path.Combine(browserPath, "Extensions", "ext.json"), "{}");
        File.WriteAllText(Path.Combine(browserPath, "Bookmarks"), "{}");

        _fileSystem.DirectoryExists(browserPath).Returns(true);

        var profile = CreateProfileWithBrowser("Alice", "Chrome", "Default", browserPath);
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice - Chrome",
                ItemType = MigrationItemType.BrowserData,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "capture-exclude");
        await _service.CaptureAsync(items, outputDir);

        var capturedDir = Path.Combine(outputDir, "Alice_Chrome_Default");
        Directory.Exists(Path.Combine(capturedDir, "Cache")).Should().BeFalse();
        Directory.Exists(Path.Combine(capturedDir, "Code Cache")).Should().BeFalse();
        Directory.Exists(Path.Combine(capturedDir, "GPUCache")).Should().BeFalse();
        Directory.Exists(Path.Combine(capturedDir, "Extensions")).Should().BeTrue();
        File.Exists(Path.Combine(capturedDir, "Bookmarks")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatus()
    {
        var browserPath = Path.Combine(_tempDir, "status-test");
        Directory.CreateDirectory(browserPath);
        _fileSystem.DirectoryExists(browserPath).Returns(true);

        var profile = CreateProfileWithBrowser("Alice", "Chrome", "Default", browserPath);
        var item = new MigrationItem
        {
            DisplayName = "Alice - Chrome",
            ItemType = MigrationItemType.BrowserData,
            IsSelected = true,
            SourceData = profile
        };

        await _service.CaptureAsync(new[] { item }, Path.Combine(_tempDir, "status-out"));

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonBrowserItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "SomeApp",
                ItemType = MigrationItemType.Application,
                IsSelected = true
            }
        };

        var outputDir = Path.Combine(_tempDir, "skip-test");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "browser-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SkipsBrowserPath_WhenNotExists()
    {
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var profile = CreateProfileWithBrowser("Alice", "Chrome", "Default", @"C:\NonExistent");
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Alice - Chrome",
                ItemType = MigrationItemType.BrowserData,
                IsSelected = true,
                SourceData = profile
            }
        };

        var outputDir = Path.Combine(_tempDir, "skip-nopath");
        await _service.CaptureAsync(items, outputDir);

        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "browser-manifest.json"));
        json.Should().Contain("\"Browsers\": []");
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_CopiesBrowserDataToDestination()
    {
        var captureDir = Path.Combine(_tempDir, "restore-src");
        var browserDir = Path.Combine(captureDir, "Alice_Chrome_Default");
        Directory.CreateDirectory(browserDir);
        File.WriteAllText(Path.Combine(browserDir, "Bookmarks"), "{\"bookmarks\":[]}");

        var manifest = new BrowserCaptureManifest
        {
            Browsers =
            [
                new CapturedBrowserEntry
                {
                    BrowserName = "Chrome",
                    ProfileName = "Default",
                    SourceUsername = "Alice",
                    CaptureSubDir = "Alice_Chrome_Default"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "browser-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var destProfile = Path.Combine(_tempDir, "restore-dest");
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
                DestinationUsername = "Bob",
                DestinationProfilePath = destProfile
            }
        };

        await _service.RestoreAsync(captureDir, mappings);

        var expectedPath = Path.Combine(destProfile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Bookmarks");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_HandlesNoManifest()
    {
        var captureDir = Path.Combine(_tempDir, "no-manifest");
        Directory.CreateDirectory(captureDir);

        await _service.RestoreAsync(captureDir, new List<UserMapping>());
    }

    [Fact]
    public async Task RestoreAsync_SkipsUnmappedUsers()
    {
        var captureDir = Path.Combine(_tempDir, "unmapped");
        Directory.CreateDirectory(captureDir);

        var manifest = new BrowserCaptureManifest
        {
            Browsers =
            [
                new CapturedBrowserEntry
                {
                    BrowserName = "Chrome",
                    SourceUsername = "UnknownUser",
                    CaptureSubDir = "UnknownUser_Chrome_Default"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "browser-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "DifferentUser" },
                DestinationUsername = "Bob"
            }
        };

        await _service.RestoreAsync(captureDir, mappings);
    }

    [Fact]
    public async Task RestoreAsync_CreatesFirefoxProfilesIni()
    {
        var captureDir = Path.Combine(_tempDir, "firefox-restore");
        var browserDir = Path.Combine(captureDir, "Alice_Firefox_default-release");
        Directory.CreateDirectory(browserDir);
        File.WriteAllText(Path.Combine(browserDir, "places.sqlite"), "data");

        var manifest = new BrowserCaptureManifest
        {
            Browsers =
            [
                new CapturedBrowserEntry
                {
                    BrowserName = "Firefox",
                    ProfileName = "default-release",
                    SourceUsername = "Alice",
                    CaptureSubDir = "Alice_Firefox_default-release"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "browser-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        var destProfile = Path.Combine(_tempDir, "firefox-dest");
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { Username = "Alice", ProfilePath = @"C:\Users\Alice" },
                DestinationUsername = "Bob",
                DestinationProfilePath = destProfile
            }
        };

        await _service.RestoreAsync(captureDir, mappings);

        var iniPath = Path.Combine(destProfile, "AppData", "Roaming", "Mozilla", "Firefox", "profiles.ini");
        File.Exists(iniPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(iniPath);
        content.Should().Contain("default-release");
    }

    #endregion

    #region GetDestinationBrowserPath

    [Fact]
    public void GetDestinationBrowserPath_Chrome_ReturnsCorrectPath()
    {
        var mapping = new UserMapping
        {
            DestinationProfilePath = @"C:\Users\Bob"
        };

        var path = BrowserDataService.GetDestinationBrowserPath("Chrome", "Default", mapping);

        path.Should().Be(@"C:\Users\Bob\AppData\Local\Google\Chrome\User Data\Default");
    }

    [Fact]
    public void GetDestinationBrowserPath_Edge_ReturnsCorrectPath()
    {
        var mapping = new UserMapping
        {
            DestinationProfilePath = @"C:\Users\Bob"
        };

        var path = BrowserDataService.GetDestinationBrowserPath("Edge", "Default", mapping);

        path.Should().Be(@"C:\Users\Bob\AppData\Local\Microsoft\Edge\User Data\Default");
    }

    [Fact]
    public void GetDestinationBrowserPath_Firefox_ReturnsCorrectPath()
    {
        var mapping = new UserMapping
        {
            DestinationProfilePath = @"C:\Users\Bob"
        };

        var path = BrowserDataService.GetDestinationBrowserPath("Firefox", "abc123.default-release", mapping);

        path.Should().Be(@"C:\Users\Bob\AppData\Roaming\Mozilla\Firefox\Profiles\abc123.default-release");
    }

    [Fact]
    public void GetDestinationBrowserPath_UnknownBrowser_ReturnsNull()
    {
        var mapping = new UserMapping
        {
            DestinationProfilePath = @"C:\Users\Bob"
        };

        BrowserDataService.GetDestinationBrowserPath("Brave", "Default", mapping)
            .Should().BeNull();
    }

    #endregion

    #region CopyDirectorySelective

    [Fact]
    public void CopyDirectorySelective_ExcludesSpecifiedPatterns()
    {
        var src = Path.Combine(_tempDir, "sel-src");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(Path.Combine(src, "Keep"));
        Directory.CreateDirectory(Path.Combine(src, "Exclude"));
        File.WriteAllText(Path.Combine(src, "Keep", "file.txt"), "keep");
        File.WriteAllText(Path.Combine(src, "Exclude", "file.txt"), "exclude");

        var dest = Path.Combine(_tempDir, "sel-dest");
        BrowserDataService.CopyDirectorySelective(src, dest, ["Exclude"]);

        Directory.Exists(Path.Combine(dest, "Keep")).Should().BeTrue();
        Directory.Exists(Path.Combine(dest, "Exclude")).Should().BeFalse();
    }

    #endregion
}
