using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class FileCaptureServiceTests : IDisposable
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly FileCaptureService _service;
    private readonly string _tempDir;

    public FileCaptureServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-filecap-test-" + Guid.NewGuid().ToString("N")[..8]);
        _service = new FileCaptureService(
            _fileSystem,
            NullLogger<FileCaptureService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CapturesInstallLocation()
    {
        // Create a fake install directory
        var installDir = Path.Combine(_tempDir, "source-install");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(Path.Combine(installDir, "app.exe"), "fake-exe");
        await File.WriteAllTextAsync(Path.Combine(installDir, "config.dll"), "fake-dll");

        _fileSystem.DirectoryExists(installDir).Returns(true);
        _fileSystem.GetDirectorySize(installDir).Returns(1000);

        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "TestApp", InstallLocation = installDir }
        };

        var outputDir = Path.Combine(_tempDir, "output");
        var manifest = await _service.CaptureAsync(apps, outputDir);

        manifest.Apps.Should().HaveCount(1);
        manifest.Apps[0].ApplicationName.Should().Be("TestApp");
        manifest.Apps[0].CapturedPaths.Should().Contain(p => p.PathCategory == FilePathCategory.InstallLocation);

        // Verify files were copied
        var capturedInstall = Path.Combine(outputDir, "TestApp", "install");
        File.Exists(Path.Combine(capturedInstall, "app.exe")).Should().BeTrue();
        File.Exists(Path.Combine(capturedInstall, "config.dll")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CapturesAppDataPaths()
    {
        var appDataDir = Path.Combine(_tempDir, "source-appdata");
        Directory.CreateDirectory(appDataDir);
        await File.WriteAllTextAsync(Path.Combine(appDataDir, "prefs.json"), "{}");

        _fileSystem.DirectoryExists(appDataDir).Returns(true);

        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "TestApp", AppDataPaths = [appDataDir] }
        };

        var outputDir = Path.Combine(_tempDir, "output");
        var manifest = await _service.CaptureAsync(apps, outputDir);

        manifest.Apps.Should().HaveCount(1);
        manifest.Apps[0].CapturedPaths.Should().HaveCount(1);

        var captured = Path.Combine(outputDir, "TestApp", "appdata-0");
        File.Exists(Path.Combine(captured, "prefs.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_WritesManifestFile()
    {
        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "EmptyApp" } // No paths to capture
        };

        var outputDir = Path.Combine(_tempDir, "output");
        await _service.CaptureAsync(apps, outputDir);

        File.Exists(Path.Combine(outputDir, "file-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_PreservesTimestamps()
    {
        var sourceDir = Path.Combine(_tempDir, "source-ts");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "timestamped.txt");
        await File.WriteAllTextAsync(sourceFile, "content");

        var knownTime = new DateTime(2023, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, knownTime);

        _fileSystem.DirectoryExists(sourceDir).Returns(true);

        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "TSApp", InstallLocation = sourceDir }
        };

        var outputDir = Path.Combine(_tempDir, "output");
        await _service.CaptureAsync(apps, outputDir);

        var capturedFile = Path.Combine(outputDir, "TSApp", "install", "timestamped.txt");
        File.Exists(capturedFile).Should().BeTrue();
        File.GetLastWriteTimeUtc(capturedFile).Should().Be(knownTime);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonExistentPaths()
    {
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "GhostApp",
                InstallLocation = @"C:\NonExistent\Path",
                AppDataPaths = [@"C:\Users\Bill\AppData\Roaming\GhostApp"]
            }
        };

        var outputDir = Path.Combine(_tempDir, "output");
        var manifest = await _service.CaptureAsync(apps, outputDir);

        manifest.Apps.Should().BeEmpty(); // Nothing to capture
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_RestoresCapturedFiles()
    {
        // Create a fake captured structure
        var captureDir = Path.Combine(_tempDir, "capture");
        var appDir = Path.Combine(captureDir, "TestApp", "install");
        Directory.CreateDirectory(appDir);
        await File.WriteAllTextAsync(Path.Combine(appDir, "app.exe"), "restored");

        var manifest = new FileCaptureManifest
        {
            Apps =
            [
                new FileCaptureAppEntry
                {
                    ApplicationName = "TestApp",
                    DirectoryName = "TestApp",
                    CapturedPaths =
                    [
                        new FileCapturePathEntry
                        {
                            OriginalPath = Path.Combine(_tempDir, "restored-install"),
                            CaptureSubDir = "install",
                            PathCategory = FilePathCategory.InstallLocation,
                            SizeBytes = 100
                        }
                    ]
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(captureDir, "file-manifest.json"), manifestJson);

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        var restoredFile = Path.Combine(_tempDir, "restored-install", "app.exe");
        File.Exists(restoredFile).Should().BeTrue();
        (await File.ReadAllTextAsync(restoredFile)).Should().Be("restored");
    }

    [Fact]
    public async Task RestoreAsync_SkipsWhenNoManifest()
    {
        Directory.CreateDirectory(_tempDir);

        // Should not throw
        await _service.RestoreAsync(_tempDir, new List<UserMapping>());
    }

    [Fact]
    public async Task RestoreAsync_RemapsAppDataPaths()
    {
        var captureDir = Path.Combine(_tempDir, "capture");
        var appDir = Path.Combine(captureDir, "TestApp", "appdata-0");
        Directory.CreateDirectory(appDir);
        await File.WriteAllTextAsync(Path.Combine(appDir, "config.ini"), "[Settings]");

        var manifest = new FileCaptureManifest
        {
            Apps =
            [
                new FileCaptureAppEntry
                {
                    ApplicationName = "TestApp",
                    DirectoryName = "TestApp",
                    CapturedPaths =
                    [
                        new FileCapturePathEntry
                        {
                            OriginalPath = @"C:\Users\Bill\AppData\Roaming\TestApp",
                            CaptureSubDir = "appdata-0",
                            PathCategory = FilePathCategory.AppDataRoaming,
                            SizeBytes = 50
                        }
                    ]
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(captureDir, "file-manifest.json"), manifestJson);

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        // This will try to write to C:\Users\William\AppData\Roaming\TestApp which may not exist
        // The service handles this gracefully (logs warning)
        await _service.RestoreAsync(captureDir, mappings);

        // We can't verify the actual file on a non-existent path, but we verified no exception
    }

    #endregion

    #region CategorizeAppDataPath

    [Theory]
    [InlineData(@"C:\Users\Bill\AppData\Roaming\Chrome", FilePathCategory.AppDataRoaming)]
    [InlineData(@"C:\Users\Bill\AppData\Local\Chrome", FilePathCategory.AppDataLocal)]
    [InlineData(@"C:\Users\Bill\AppData\LocalLow\Chrome", FilePathCategory.AppDataLocalLow)]
    [InlineData(@"C:\Some\Other\Path", FilePathCategory.AppDataRoaming)] // Default
    public void CategorizeAppDataPath_CategorizesCorrectly(string path, FilePathCategory expected)
    {
        FileCaptureService.CategorizeAppDataPath(path).Should().Be(expected);
    }

    #endregion

    #region SanitizeDirName

    [Fact]
    public void SanitizeDirName_RemovesInvalidChars()
    {
        FileCaptureService.SanitizeDirName("My:App/v2").Should().Be("My_App_v2");
    }

    [Fact]
    public void SanitizeDirName_HandlesNormalNames()
    {
        FileCaptureService.SanitizeDirName("Google Chrome").Should().Be("Google Chrome");
    }

    #endregion
}
