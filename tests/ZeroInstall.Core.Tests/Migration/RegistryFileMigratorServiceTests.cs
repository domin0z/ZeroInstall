using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class RegistryFileMigratorServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly RegistryCaptureService _registryCapture;
    private readonly FileCaptureService _fileCapture;
    private readonly RegistryFileMigratorService _service;
    private readonly string _tempDir;

    public RegistryFileMigratorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-rfmig-test-" + Guid.NewGuid().ToString("N")[..8]);
        _registryCapture = new RegistryCaptureService(
            _processRunner, NullLogger<RegistryCaptureService>.Instance);
        _fileCapture = new FileCaptureService(
            _fileSystem, NullLogger<FileCaptureService>.Instance);
        _service = new RegistryFileMigratorService(
            _registryCapture, _fileCapture, _processRunner, _fileSystem,
            NullLogger<RegistryFileMigratorService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesTier2Manifest()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "CustomApp",
                ItemType = MigrationItemType.Application,
                IsSelected = true,
                RecommendedTier = MigrationTier.RegistryFile,
                SourceData = new DiscoveredApplication
                {
                    Name = "CustomApp",
                    Publisher = "CustomVendor"
                }
            }
        };

        await _service.CaptureAsync(items, _tempDir);

        File.Exists(Path.Combine(_tempDir, "tier2-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatus()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var item = new MigrationItem
        {
            DisplayName = "TestApp",
            ItemType = MigrationItemType.Application,
            IsSelected = true,
            RecommendedTier = MigrationTier.RegistryFile,
            SourceData = new DiscoveredApplication { Name = "TestApp" }
        };

        await _service.CaptureAsync(new[] { item }, _tempDir);

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonRegistryFileItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "PackageApp",
                IsSelected = true,
                RecommendedTier = MigrationTier.Package,
                SourceData = new DiscoveredApplication { Name = "PackageApp", WingetPackageId = "Test.App" }
            }
        };

        await _service.CaptureAsync(items, _tempDir);

        // Should not create tier2 manifest since no RegistryFile items
        File.Exists(Path.Combine(_tempDir, "tier2-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_DetectsLicensingWarnings()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Adobe Photoshop",
                IsSelected = true,
                RecommendedTier = MigrationTier.RegistryFile,
                SourceData = new DiscoveredApplication
                {
                    Name = "Adobe Photoshop CC 2024",
                    Publisher = "Adobe Inc."
                }
            }
        };

        await _service.CaptureAsync(items, _tempDir);

        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "tier2-manifest.json"));
        json.Should().Contain("Adobe Photoshop");
        json.Should().Contain("LicensingWarnings");
    }

    #endregion

    #region DetectLicensingApps

    [Fact]
    public void DetectLicensingApps_DetectsKnownVendors()
    {
        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "Adobe Photoshop CC", Publisher = "Adobe Inc." },
            new() { Name = "Microsoft Office 365", Publisher = "Microsoft Corporation" },
            new() { Name = "Notepad++", Publisher = "Don Ho" }
        };

        var warnings = RegistryFileMigratorService.DetectLicensingApps(apps);

        warnings.Should().Contain("Adobe Photoshop CC");
        warnings.Should().Contain("Microsoft Office 365");
        warnings.Should().NotContain("Notepad++");
    }

    [Fact]
    public void DetectLicensingApps_EmptyList_ReturnsEmpty()
    {
        var warnings = RegistryFileMigratorService.DetectLicensingApps(new List<DiscoveredApplication>());
        warnings.Should().BeEmpty();
    }

    #endregion

    #region DetectComRegistrations

    [Fact]
    public void DetectComRegistrations_DetectsKnownPublishers()
    {
        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "Word", Publisher = "Microsoft Corporation" },
            new() { Name = "7-Zip", Publisher = "Igor Pavlov" }
        };

        var coms = RegistryFileMigratorService.DetectComRegistrations(apps);

        coms.Should().Contain("Word");
        coms.Should().NotContain("7-Zip");
    }

    #endregion

    #region FindMainExecutable

    [Fact]
    public void FindMainExecutable_SingleExe_ReturnsThatExe()
    {
        var exes = new[] { @"C:\Program Files\App\app.exe" };

        var result = RegistryFileMigratorService.FindMainExecutable(exes, "App");

        result.Should().Be(@"C:\Program Files\App\app.exe");
    }

    [Fact]
    public void FindMainExecutable_MatchesAppName()
    {
        var exes = new[]
        {
            @"C:\Program Files\App\uninstall.exe",
            @"C:\Program Files\App\helper.exe",
            @"C:\Program Files\App\SuperEditor.exe"
        };

        var result = RegistryFileMigratorService.FindMainExecutable(exes, "SuperEditor");

        result.Should().Be(@"C:\Program Files\App\SuperEditor.exe");
    }

    [Fact]
    public void FindMainExecutable_FiltersNonMainExes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "zim-exe-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create files with different sizes
            var uninstall = Path.Combine(tempDir, "uninstall.exe");
            var main = Path.Combine(tempDir, "myapp.exe");
            var update = Path.Combine(tempDir, "update.exe");

            File.WriteAllBytes(uninstall, new byte[100]);
            File.WriteAllBytes(main, new byte[5000]);
            File.WriteAllBytes(update, new byte[200]);

            var exes = new[] { uninstall, main, update };

            var result = RegistryFileMigratorService.FindMainExecutable(exes, "SomethingElse");

            // Should pick myapp.exe — uninstall and update are filtered, myapp is largest remaining
            result.Should().Be(main);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FindMainExecutable_EmptyArray_ReturnsNull()
    {
        var result = RegistryFileMigratorService.FindMainExecutable(Array.Empty<string>(), "App");
        result.Should().BeNull();
    }

    #endregion
}
