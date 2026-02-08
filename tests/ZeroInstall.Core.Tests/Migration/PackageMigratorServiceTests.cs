using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class PackageMigratorServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly AppDataCaptureHelper _appDataHelper;
    private readonly PackageMigratorService _service;
    private readonly string _tempDir;

    public PackageMigratorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-pkg-test-" + Guid.NewGuid().ToString("N")[..8]);
        _appDataHelper = new AppDataCaptureHelper(
            _fileSystem, _processRunner,
            NullLogger<AppDataCaptureHelper>.Instance);
        _service = new PackageMigratorService(
            _processRunner, _fileSystem, _appDataHelper,
            NullLogger<PackageMigratorService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region ResolvePackagesAsync

    [Fact]
    public async Task ResolvePackagesAsync_PrefersWingetOverChocolatey()
    {
        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "Google Chrome",
                Version = "120.0",
                WingetPackageId = "Google.Chrome",
                ChocolateyPackageId = "googlechrome"
            }
        };

        var packages = await _service.ResolvePackagesAsync(apps);

        packages.Should().HaveCount(1);
        packages[0].PackageManager.Should().Be("winget");
        packages[0].PackageId.Should().Be("Google.Chrome");
    }

    [Fact]
    public async Task ResolvePackagesAsync_FallsBackToChocolatey()
    {
        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "7-Zip",
                Version = "23.01",
                ChocolateyPackageId = "7zip"
            }
        };

        var packages = await _service.ResolvePackagesAsync(apps);

        packages.Should().HaveCount(1);
        packages[0].PackageManager.Should().Be("chocolatey");
        packages[0].PackageId.Should().Be("7zip");
    }

    [Fact]
    public async Task ResolvePackagesAsync_SkipsAppsWithNoPackageId()
    {
        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "Custom App", Version = "1.0" },
            new() { Name = "Chrome", Version = "120.0", WingetPackageId = "Google.Chrome" }
        };

        var packages = await _service.ResolvePackagesAsync(apps);

        packages.Should().HaveCount(1);
        packages[0].ApplicationName.Should().Be("Chrome");
    }

    [Fact]
    public async Task ResolvePackagesAsync_EmptyList_ReturnsEmpty()
    {
        var packages = await _service.ResolvePackagesAsync(new List<DiscoveredApplication>());

        packages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolvePackagesAsync_PreservesVersionInfo()
    {
        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "VS Code",
                Version = "1.85.0",
                WingetPackageId = "Microsoft.VisualStudioCode"
            }
        };

        var packages = await _service.ResolvePackagesAsync(apps);

        packages[0].Version.Should().Be("1.85.0");
        packages[0].ApplicationName.Should().Be("VS Code");
    }

    #endregion

    #region InstallPackagesAsync

    [Fact]
    public async Task InstallPackagesAsync_RunsWingetInstall()
    {
        // winget is available
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "v1.7" });
        _processRunner.RunAsync("choco", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        // winget install succeeds
        _processRunner.RunAsync("winget", Arg.Is<string>(s => s.Contains("Google.Chrome")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "Successfully installed" });

        var packages = new List<PackageInstallEntry>
        {
            new()
            {
                ApplicationName = "Chrome",
                PackageManager = "winget",
                PackageId = "Google.Chrome",
                Version = "120.0"
            }
        };

        await _service.InstallPackagesAsync(packages);

        await _processRunner.Received(1).RunAsync("winget",
            Arg.Is<string>(s => s.Contains("Google.Chrome") && s.Contains("--version")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallPackagesAsync_RunsChocoInstall()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });
        _processRunner.RunAsync("choco", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "2.2.0" });

        _processRunner.RunAsync("choco", Arg.Is<string>(s => s.Contains("7zip")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "Installed" });

        var packages = new List<PackageInstallEntry>
        {
            new()
            {
                ApplicationName = "7-Zip",
                PackageManager = "chocolatey",
                PackageId = "7zip"
            }
        };

        await _service.InstallPackagesAsync(packages);

        await _processRunner.Received(1).RunAsync("choco",
            Arg.Is<string>(s => s.Contains("7zip") && s.Contains("--yes")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallPackagesAsync_HandlesInstallFailure()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "v1.7" });
        _processRunner.RunAsync("choco", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        _processRunner.RunAsync("winget", Arg.Is<string>(s => s.Contains("Fake.Package")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "No package found" });

        var packages = new List<PackageInstallEntry>
        {
            new()
            {
                ApplicationName = "Fake App",
                PackageManager = "winget",
                PackageId = "Fake.Package"
            }
        };

        // Should not throw — failure is logged, not thrown
        await _service.InstallPackagesAsync(packages);
    }

    [Fact]
    public async Task InstallPackagesAsync_ReportsProgress()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "v1.7" });
        _processRunner.RunAsync("choco", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });
        _processRunner.RunAsync("winget", Arg.Is<string>(s => s.Contains("install")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        var packages = new List<PackageInstallEntry>
        {
            new() { ApplicationName = "App1", PackageManager = "winget", PackageId = "Test.App1" },
            new() { ApplicationName = "App2", PackageManager = "winget", PackageId = "Test.App2" }
        };

        await _service.InstallPackagesAsync(packages, progress);

        await Task.Delay(50); // Allow Progress<T> to propagate
        progressReports.Should().HaveCountGreaterOrEqualTo(2);
        progressReports[0].TotalItems.Should().Be(2);
        progressReports[0].CurrentItemIndex.Should().Be(1);
    }

    [Fact]
    public async Task InstallPackagesAsync_CanBeCancelled()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });
        _processRunner.RunAsync("choco", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var packages = new List<PackageInstallEntry>
        {
            new() { ApplicationName = "App1", PackageManager = "winget", PackageId = "Test.App1" }
        };

        var act = () => _service.InstallPackagesAsync(packages, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region InstallViaWinget / InstallViaChocolatey

    [Fact]
    public async Task InstallViaWingetAsync_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("winget", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "Installed" });

        var package = new PackageInstallEntry
        {
            ApplicationName = "Notepad++",
            PackageManager = "winget",
            PackageId = "Notepad++.Notepad++",
            Version = "8.6"
        };

        var result = await _service.InstallViaWingetAsync(package, CancellationToken.None);

        result.Success.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("winget",
            Arg.Is<string>(s =>
                s.Contains("--id \"Notepad++.Notepad++\"") &&
                s.Contains("--version \"8.6\"") &&
                s.Contains("--accept-package-agreements") &&
                s.Contains("--disable-interactivity")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallViaWingetAsync_OmitsVersionWhenNull()
    {
        _processRunner.RunAsync("winget", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var package = new PackageInstallEntry
        {
            ApplicationName = "App",
            PackageManager = "winget",
            PackageId = "Test.App"
        };

        await _service.InstallViaWingetAsync(package, CancellationToken.None);

        await _processRunner.Received(1).RunAsync("winget",
            Arg.Is<string>(s => !s.Contains("--version")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallViaChocolateyAsync_BuildsCorrectCommand()
    {
        _processRunner.RunAsync("choco", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "Installed" });

        var package = new PackageInstallEntry
        {
            ApplicationName = "Firefox",
            PackageManager = "chocolatey",
            PackageId = "firefox",
            Version = "121.0"
        };

        var result = await _service.InstallViaChocolateyAsync(package, CancellationToken.None);

        result.Success.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("choco",
            Arg.Is<string>(s =>
                s.Contains("install firefox") &&
                s.Contains("--yes") &&
                s.Contains("--version 121.0")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region IsPackageManagerAvailable

    [Fact]
    public async Task IsPackageManagerAvailableAsync_WhenSucceeds_ReturnsTrue()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "v1.7.0" });

        var result = await _service.IsPackageManagerAvailableAsync("winget", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPackageManagerAvailableAsync_WhenFails_ReturnsFalse()
    {
        _processRunner.RunAsync("winget", "--version", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var result = await _service.IsPackageManagerAvailableAsync("winget", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPackageManagerAvailableAsync_WhenThrows_ReturnsFalse()
    {
        _processRunner.RunAsync("nonexistent", "--version", Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(x => throw new System.ComponentModel.Win32Exception("not found"));

        var result = await _service.IsPackageManagerAvailableAsync("nonexistent", CancellationToken.None);

        result.Should().BeFalse();
    }

    #endregion

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesManifestFile()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Chrome",
                ItemType = MigrationItemType.Application,
                IsSelected = true,
                RecommendedTier = MigrationTier.Package,
                SourceData = new DiscoveredApplication
                {
                    Name = "Google Chrome",
                    Version = "120.0",
                    WingetPackageId = "Google.Chrome"
                }
            }
        };

        // reg export will fail (no real registry) — that's OK
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.CaptureAsync(items, _tempDir);

        var manifestPath = Path.Combine(_tempDir, "package-capture-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().Contain("Google.Chrome");
        json.Should().Contain("winget");
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatusOnSuccess()
    {
        var item = new MigrationItem
        {
            DisplayName = "Chrome",
            ItemType = MigrationItemType.Application,
            IsSelected = true,
            RecommendedTier = MigrationTier.Package,
            SourceData = new DiscoveredApplication
            {
                Name = "Google Chrome",
                Version = "120.0",
                WingetPackageId = "Google.Chrome"
            }
        };

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.CaptureAsync(new[] { item }, _tempDir);

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonSelectedItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Unselected App",
                ItemType = MigrationItemType.Application,
                IsSelected = false,
                RecommendedTier = MigrationTier.Package,
                SourceData = new DiscoveredApplication { Name = "Unselected", WingetPackageId = "Test.App" }
            }
        };

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.CaptureAsync(items, _tempDir);

        var manifestPath = Path.Combine(_tempDir, "package-capture-manifest.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().NotContain("Unselected");
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonPackageTierItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Registry App",
                ItemType = MigrationItemType.Application,
                IsSelected = true,
                RecommendedTier = MigrationTier.RegistryFile,
                SourceData = new DiscoveredApplication { Name = "RegApp" }
            }
        };

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.CaptureAsync(items, _tempDir);

        var manifestPath = Path.Combine(_tempDir, "package-capture-manifest.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().NotContain("RegApp");
    }

    #endregion

    #region SanitizeDirectoryName

    [Fact]
    public void SanitizeDirectoryName_RemovesInvalidChars()
    {
        PackageMigratorService.SanitizeDirectoryName("My App: v2.0").Should().Be("My App_ v2.0");
    }

    [Fact]
    public void SanitizeDirectoryName_HandlesNormalNames()
    {
        PackageMigratorService.SanitizeDirectoryName("Google Chrome").Should().Be("Google Chrome");
    }

    [Fact]
    public void SanitizeDirectoryName_TrimsTrailingDots()
    {
        PackageMigratorService.SanitizeDirectoryName("SomeApp...").Should().Be("SomeApp");
    }

    #endregion
}
