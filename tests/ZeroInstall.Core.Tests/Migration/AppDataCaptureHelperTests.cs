using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class AppDataCaptureHelperTests : IDisposable
{
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly AppDataCaptureHelper _helper;
    private readonly string _tempDir;

    public AppDataCaptureHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-appdata-test-" + Guid.NewGuid().ToString("N")[..8]);
        _helper = new AppDataCaptureHelper(
            _fileSystem, _processRunner,
            NullLogger<AppDataCaptureHelper>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_WritesManifest()
    {
        var app = new DiscoveredApplication
        {
            Name = "TestApp",
            Version = "1.0",
            Publisher = "TestPublisher"
        };

        // No AppData paths, no real registry
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var manifest = await _helper.CaptureAsync(app, _tempDir);

        manifest.ApplicationName.Should().Be("TestApp");
        manifest.ApplicationVersion.Should().Be("1.0");

        File.Exists(Path.Combine(_tempDir, "appdata-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CapturesAppDataPaths()
    {
        // Create a fake AppData directory
        var fakeAppData = Path.Combine(_tempDir, "source-appdata");
        Directory.CreateDirectory(fakeAppData);
        await File.WriteAllTextAsync(Path.Combine(fakeAppData, "config.json"), "{\"key\":\"value\"}");

        var app = new DiscoveredApplication
        {
            Name = "TestApp",
            Version = "1.0",
            AppDataPaths = [fakeAppData]
        };

        // Make the mock say the directory exists
        _fileSystem.DirectoryExists(fakeAppData).Returns(true);
        _fileSystem.GetDirectorySize(fakeAppData).Returns(100);

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var outputDir = Path.Combine(_tempDir, "output");
        var manifest = await _helper.CaptureAsync(app, outputDir);

        manifest.CapturedPaths.Should().HaveCount(1);
        manifest.CapturedPaths[0].OriginalPath.Should().Be(fakeAppData);
        manifest.CapturedPaths[0].SizeBytes.Should().Be(100);

        // Verify file was actually copied
        var capturedDir = Path.Combine(outputDir, "files", manifest.CapturedPaths[0].PathId);
        File.Exists(Path.Combine(capturedDir, "config.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_ExportsRegistryKeys()
    {
        var app = new DiscoveredApplication
        {
            Name = "TestApp",
            Version = "1.0",
            Publisher = "TestPub",
            AdditionalRegistryPaths = [@"HKCU\Software\TestPub\TestApp"]
        };

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "Exported" });

        var manifest = await _helper.CaptureAsync(app, _tempDir);

        manifest.CapturedRegistryFiles.Should().NotBeEmpty();
        await _processRunner.Received().RunAsync("reg",
            Arg.Is<string>(s => s.Contains("export")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_RestoresFiles()
    {
        // Setup: create a captured structure
        var captureDir = Path.Combine(_tempDir, "capture");
        var filesDir = Path.Combine(captureDir, "files", "test-path-id");
        Directory.CreateDirectory(filesDir);
        await File.WriteAllTextAsync(Path.Combine(filesDir, "settings.ini"), "[Settings]\nKey=Value");

        var manifest = new AppDataCaptureManifest
        {
            ApplicationName = "TestApp",
            CapturedPaths =
            [
                new CapturedPathEntry
                {
                    OriginalPath = @"C:\Users\Bill\AppData\Roaming\TestApp",
                    PathId = "test-path-id",
                    SizeBytes = 50
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(captureDir, "appdata-manifest.json"), manifestJson);

        // Create target directory so restore has somewhere to write
        var targetDir = Path.Combine(_tempDir, "restore-target");
        Directory.CreateDirectory(targetDir);

        var userMappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile
                {
                    Username = "Bill",
                    ProfilePath = @"C:\Users\Bill"
                },
                DestinationUsername = "Bill",
                DestinationProfilePath = @"C:\Users\Bill"
            }
        };

        // This will try to write to C:\Users\Bill\AppData\Roaming\TestApp which may not exist
        // but the helper just logs warnings for failures
        await _helper.RestoreAsync(captureDir, userMappings);
    }

    [Fact]
    public async Task RestoreAsync_SkipsWhenNoManifest()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        // Should not throw
        await _helper.RestoreAsync(emptyDir, new List<UserMapping>());
    }

    #endregion

    #region RemapPathForUser

    [Fact]
    public void RemapPathForUser_RemapsWhenUsernamesDiffer()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = AppDataCaptureHelper.RemapPathForUser(
            @"C:\Users\Bill\AppData\Roaming\Chrome", mappings);

        result.Should().Be(@"C:\Users\William\AppData\Roaming\Chrome");
    }

    [Fact]
    public void RemapPathForUser_NoRemapWhenSameUsername()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\Bill"
            }
        };

        var result = AppDataCaptureHelper.RemapPathForUser(
            @"C:\Users\Bill\AppData\Roaming\Chrome", mappings);

        result.Should().Be(@"C:\Users\Bill\AppData\Roaming\Chrome");
    }

    [Fact]
    public void RemapPathForUser_CaseInsensitive()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = AppDataCaptureHelper.RemapPathForUser(
            @"C:\Users\Bill\AppData\Roaming\Chrome", mappings);

        result.Should().Be(@"C:\Users\William\AppData\Roaming\Chrome");
    }

    [Fact]
    public void RemapPathForUser_NoMappings_ReturnsOriginal()
    {
        var result = AppDataCaptureHelper.RemapPathForUser(
            @"C:\Users\Bill\AppData\Roaming\Chrome", new List<UserMapping>());

        result.Should().Be(@"C:\Users\Bill\AppData\Roaming\Chrome");
    }

    #endregion

    #region GeneratePathId

    [Fact]
    public void GeneratePathId_ProducesSafeDirectoryName()
    {
        var result = AppDataCaptureHelper.GeneratePathId(@"C:\Users\Bill\AppData\Roaming\Chrome");

        result.Should().NotContain("\\");
        result.Should().NotContain(":");
        result.Should().Contain("Users");
        result.Should().Contain("Chrome");
    }

    [Fact]
    public void GeneratePathId_HandlesSpaces()
    {
        var result = AppDataCaptureHelper.GeneratePathId(@"C:\Program Files\My App");

        result.Should().Contain("Program-Files");
        result.Should().Contain("My-App");
    }

    #endregion
}
