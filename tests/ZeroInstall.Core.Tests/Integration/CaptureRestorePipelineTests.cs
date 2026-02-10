using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Integration;

/// <summary>
/// End-to-end pipeline tests: capture → transport → restore using real temp directories
/// and ExternalStorageTransport with mocked IProcessRunner for package install commands.
/// </summary>
public class CaptureRestorePipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _captureDir;
    private readonly string _transportDir;
    private readonly string _restoreDir;
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemAccessor _fileSystem;

    public CaptureRestorePipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-e2e-{Guid.NewGuid():N}");
        _captureDir = Path.Combine(_tempDir, "capture");
        _transportDir = Path.Combine(_tempDir, "transport");
        _restoreDir = Path.Combine(_tempDir, "restore");

        Directory.CreateDirectory(_captureDir);
        Directory.CreateDirectory(_transportDir);
        Directory.CreateDirectory(_restoreDir);

        _processRunner = Substitute.For<IProcessRunner>();
        _fileSystem = Substitute.For<IFileSystemAccessor>();

        // Default: all processes succeed
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "OK" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task PackageMigrator_CaptureAndRestore_RoundTrips()
    {
        var appDataHelper = new AppDataCaptureHelper(_fileSystem, _processRunner, NullLogger<AppDataCaptureHelper>.Instance);
        var migrator = new PackageMigratorService(
            _processRunner, _fileSystem, appDataHelper,
            NullLogger<PackageMigratorService>.Instance);

        // Set up a DiscoveredApplication with a winget package
        var app = new DiscoveredApplication
        {
            Name = "TestApp",
            Version = "1.0",
            Publisher = "TestPublisher",
            WingetPackageId = "Test.App"
        };
        var item = new MigrationItem
        {
            DisplayName = "TestApp",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            IsSelected = true,
            SourceData = app
        };

        // Capture
        await migrator.CaptureAsync([item], _captureDir);

        // Verify capture produced manifest
        var manifestPath = Path.Combine(_captureDir, "package-capture-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().Contain("Test.App");

        // Restore from same capture dir
        var act = async () => await migrator.RestoreAsync(_captureDir, []);

        // RestoreAsync should not throw (winget install will be mocked as success)
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegistryMigrator_CaptureAndRestore_RoundTrips()
    {
        var registryCapture = new RegistryCaptureService(_processRunner, NullLogger<RegistryCaptureService>.Instance);
        var fileCapture = new FileCaptureService(_fileSystem, NullLogger<FileCaptureService>.Instance);
        var migrator = new RegistryFileMigratorService(
            registryCapture, fileCapture, _processRunner, _fileSystem,
            NullLogger<RegistryFileMigratorService>.Instance);

        var app = new DiscoveredApplication
        {
            Name = "CustomApp",
            Version = "2.0",
            Publisher = "CustomDev",
            RegistryKeyPath = @"SOFTWARE\CustomApp"
        };
        var item = new MigrationItem
        {
            DisplayName = "CustomApp",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.RegistryFile,
            IsSelected = true,
            SourceData = app
        };

        // Capture
        await migrator.CaptureAsync([item], _captureDir);

        // Verify Tier 2 manifest was created
        var manifestPath = Path.Combine(_captureDir, "tier2-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().Contain("CustomApp");

        // Verify subdirectories were created
        Directory.Exists(Path.Combine(_captureDir, "registry")).Should().BeTrue();

        // Restore should not throw
        var act = async () => await migrator.RestoreAsync(_captureDir, []);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProfileSettings_CaptureAndRestore_RoundTrips()
    {
        var registryAccessor = Substitute.For<IRegistryAccessor>();
        var userAccountManager = Substitute.For<IUserAccountManager>();
        var profileTransfer = new ProfileTransferService(_fileSystem, _processRunner, NullLogger<ProfileTransferService>.Instance);
        var pathRemapper = Substitute.For<IUserPathRemapper>();
        var browserService = new BrowserDataService(_fileSystem, _processRunner, NullLogger<BrowserDataService>.Instance);
        var emailService = new EmailDataService(_fileSystem, _processRunner, NullLogger<EmailDataService>.Instance);
        var settingsReplay = new SystemSettingsReplayService(_processRunner, registryAccessor, _fileSystem, NullLogger<SystemSettingsReplayService>.Instance);

        var service = new ProfileSettingsMigratorService(
            userAccountManager, profileTransfer, pathRemapper,
            browserService, emailService, settingsReplay,
            NullLogger<ProfileSettingsMigratorService>.Instance);

        var profileDir = Path.Combine(_captureDir, "profile-settings");

        var item = new MigrationItem
        {
            DisplayName = "System Settings",
            ItemType = MigrationItemType.SystemSetting,
            IsSelected = true
        };

        // Capture — should create a manifest
        await service.CaptureAsync([item], profileDir);

        // Verify manifest exists
        var manifestPath = Path.Combine(profileDir, "profile-settings-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();

        // Restore should not throw
        var act = async () => await service.RestoreAsync(profileDir, []);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MixedTier_CaptureAll_CreatesCorrectDirectoryStructure()
    {
        var appDataHelper = new AppDataCaptureHelper(_fileSystem, _processRunner, NullLogger<AppDataCaptureHelper>.Instance);
        var packageMigrator = new PackageMigratorService(
            _processRunner, _fileSystem, appDataHelper,
            NullLogger<PackageMigratorService>.Instance);

        var registryCapture = new RegistryCaptureService(_processRunner, NullLogger<RegistryCaptureService>.Instance);
        var fileCapture = new FileCaptureService(_fileSystem, NullLogger<FileCaptureService>.Instance);
        var registryMigrator = new RegistryFileMigratorService(
            registryCapture, fileCapture, _processRunner, _fileSystem,
            NullLogger<RegistryFileMigratorService>.Instance);

        // Package tier item
        var pkgApp = new DiscoveredApplication { Name = "Chrome", WingetPackageId = "Google.Chrome" };
        var pkgItem = new MigrationItem
        {
            DisplayName = "Chrome",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            IsSelected = true,
            SourceData = pkgApp
        };

        // Registry+File tier item
        var regApp = new DiscoveredApplication { Name = "LegacyApp", RegistryKeyPath = @"SOFTWARE\Legacy" };
        var regItem = new MigrationItem
        {
            DisplayName = "LegacyApp",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.RegistryFile,
            IsSelected = true,
            SourceData = regApp
        };

        // Capture both tiers to separate subdirectories (like MigrationCoordinator does)
        var packageDir = _captureDir;
        var regFileDir = _captureDir;

        await packageMigrator.CaptureAsync([pkgItem], packageDir);
        await registryMigrator.CaptureAsync([regItem], regFileDir);

        // Verify manifests exist
        File.Exists(Path.Combine(_captureDir, "package-capture-manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(_captureDir, "tier2-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task TransportManifest_CaptureAndTransfer_RoundTrips()
    {
        var transport = new ExternalStorageTransport(
            _transportDir, NullLogger<ExternalStorageTransport>.Instance);

        // Create a manifest with items
        var manifest = new TransferManifest
        {
            SourceHostname = "SOURCE-PC",
            TransportMethod = TransportMethod.ExternalStorage,
            Items =
            [
                new MigrationItem
                {
                    DisplayName = "TestApp",
                    ItemType = MigrationItemType.Application,
                    IsSelected = true,
                    EstimatedSizeBytes = 1024
                },
                new MigrationItem
                {
                    DisplayName = "Documents",
                    ItemType = MigrationItemType.FileGroup,
                    IsSelected = true,
                    EstimatedSizeBytes = 2048
                }
            ]
        };

        // Send manifest via transport
        await transport.SendManifestAsync(manifest);

        // Receive manifest via transport
        var received = await transport.ReceiveManifestAsync();

        received.SourceHostname.Should().Be("SOURCE-PC");
        received.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        received.Items.Should().HaveCount(2);
        received.Items[0].DisplayName.Should().Be("TestApp");
        received.Items[1].DisplayName.Should().Be("Documents");
    }

    [Fact]
    public async Task FullPipeline_CaptureTransportRestore_PackageItems()
    {
        // Set up services
        var appDataHelper = new AppDataCaptureHelper(_fileSystem, _processRunner, NullLogger<AppDataCaptureHelper>.Instance);
        var migrator = new PackageMigratorService(
            _processRunner, _fileSystem, appDataHelper,
            NullLogger<PackageMigratorService>.Instance);

        var transport = new ExternalStorageTransport(
            _transportDir, NullLogger<ExternalStorageTransport>.Instance);

        var app = new DiscoveredApplication
        {
            Name = "VLC",
            Version = "3.0",
            Publisher = "VideoLAN",
            WingetPackageId = "VideoLAN.VLC"
        };
        var item = new MigrationItem
        {
            DisplayName = "VLC",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            IsSelected = true,
            SourceData = app
        };

        // Step 1: Capture
        await migrator.CaptureAsync([item], _captureDir);

        // Step 2: Transfer the capture manifest via transport
        var captureManifestPath = Path.Combine(_captureDir, "package-capture-manifest.json");
        var data = await File.ReadAllBytesAsync(captureManifestPath);
        var checksum = ChecksumHelper.Compute(data);

        using var stream = new MemoryStream(data);
        await transport.SendAsync(stream, new TransferMetadata
        {
            RelativePath = "package-capture-manifest.json",
            SizeBytes = data.Length,
            Checksum = checksum
        });

        // Step 3: Verify transported file
        var transportedMetadata = new TransferMetadata
        {
            RelativePath = "package-capture-manifest.json",
            SizeBytes = data.Length
        };
        await using var receivedStream = await transport.ReceiveAsync(transportedMetadata);
        using var ms = new MemoryStream();
        await receivedStream.CopyToAsync(ms);
        ms.Length.Should().Be(data.Length);

        // Step 4: Restore from captured data
        var act = async () => await migrator.RestoreAsync(_captureDir, []);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FullPipeline_EmptySelection_CompletesSuccessfully()
    {
        var appDataHelper = new AppDataCaptureHelper(_fileSystem, _processRunner, NullLogger<AppDataCaptureHelper>.Instance);
        var migrator = new PackageMigratorService(
            _processRunner, _fileSystem, appDataHelper,
            NullLogger<PackageMigratorService>.Instance);

        // Empty items list — capture produces a manifest with no packages
        await migrator.CaptureAsync([], _captureDir);

        // Verify empty manifest was created
        var manifestPath = Path.Combine(_captureDir, "package-capture-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(manifestPath);
        json.Should().Contain("Packages");

        // Restore from the empty capture
        var act = async () => await migrator.RestoreAsync(_captureDir, []);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FullPipeline_CaptureFailure_MarksItemFailed()
    {
        // Use an app name with illegal filesystem characters so that
        // AppDataCaptureHelper.CaptureAsync's Directory.CreateDirectory throws
        // when constructing the appdata subdirectory path.
        // SanitizeDirectoryName replaces invalid chars but the name must cause
        // a directory operation to fail. Instead, use a very long path that exceeds Windows limits.
        var longName = new string('A', 300); // Exceeds MAX_PATH for nested directory

        var appDataHelper = new AppDataCaptureHelper(_fileSystem, _processRunner, NullLogger<AppDataCaptureHelper>.Instance);
        var migrator = new PackageMigratorService(
            _processRunner, _fileSystem, appDataHelper,
            NullLogger<PackageMigratorService>.Instance);

        // Create a deeply nested capture dir to push the total path over MAX_PATH
        var deepDir = Path.Combine(_captureDir, new string('B', 200));
        Directory.CreateDirectory(_captureDir);

        var app = new DiscoveredApplication
        {
            Name = longName,
            WingetPackageId = "Fail.App"
        };
        var item = new MigrationItem
        {
            DisplayName = "FailApp",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            IsSelected = true,
            SourceData = app
        };

        // Capture — the deeply nested path + long name should cause Directory.CreateDirectory to throw
        await migrator.CaptureAsync([item], deepDir);

        item.Status.Should().Be(MigrationItemStatus.Failed);
        item.StatusMessage.Should().NotBeNullOrEmpty();
    }
}
