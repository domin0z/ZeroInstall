using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.Services;

public class MigrationCoordinatorTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly IPackageMigrator _packageMigrator = Substitute.For<IPackageMigrator>();
    private readonly IRegistryMigrator _registryMigrator = Substitute.For<IRegistryMigrator>();
    private readonly IDiskCloner _diskCloner = Substitute.For<IDiskCloner>();
    private readonly ProfileSettingsMigratorService _profileSettings;
    private readonly IJobLogger _jobLogger = Substitute.For<IJobLogger>();
    private readonly MigrationCoordinator _sut;

    public MigrationCoordinatorTests()
    {
        // Build ProfileSettingsMigratorService with all-mocked dependencies.
        // Its methods will execute but sub-services are stubs, so they do nothing.
        _profileSettings = new ProfileSettingsMigratorService(
            Substitute.For<IUserAccountManager>(),
            new ProfileTransferService(
                Substitute.For<IFileSystemAccessor>(),
                Substitute.For<IProcessRunner>(),
                Substitute.For<ILogger<ProfileTransferService>>()),
            Substitute.For<IUserPathRemapper>(),
            new BrowserDataService(
                Substitute.For<IFileSystemAccessor>(),
                Substitute.For<IProcessRunner>(),
                Substitute.For<ILogger<BrowserDataService>>()),
            new EmailDataService(
                Substitute.For<IFileSystemAccessor>(),
                Substitute.For<IProcessRunner>(),
                Substitute.For<ILogger<EmailDataService>>()),
            new SystemSettingsReplayService(
                Substitute.For<IProcessRunner>(),
                Substitute.For<IRegistryAccessor>(),
                Substitute.For<IFileSystemAccessor>(),
                Substitute.For<ILogger<SystemSettingsReplayService>>()),
            Substitute.For<ILogger<ProfileSettingsMigratorService>>());

        _jobLogger.CreateJobAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<MigrationJob>());

        _sut = new MigrationCoordinator(
            _session,
            _packageMigrator,
            _registryMigrator,
            _diskCloner,
            _profileSettings,
            _jobLogger,
            NullLogger<MigrationCoordinator>.Instance);
    }

    [Fact]
    public async Task CaptureAsync_WithPackageItems_CallsPackageMigrator()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems =
        [
            new MigrationItem
            {
                DisplayName = "Chrome",
                IsSelected = true,
                RecommendedTier = MigrationTier.Package,
                ItemType = MigrationItemType.Application
            }
        ];

        await _sut.CaptureAsync();

        await _packageMigrator.Received(1).CaptureAsync(
            Arg.Is<IReadOnlyList<MigrationItem>>(items => items.Count == 1),
            Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureAsync_WithRegFileItems_CallsRegistryMigrator()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems =
        [
            new MigrationItem
            {
                DisplayName = "Custom App",
                IsSelected = true,
                RecommendedTier = MigrationTier.RegistryFile,
                ItemType = MigrationItemType.Application
            }
        ];

        await _sut.CaptureAsync();

        await _registryMigrator.Received(1).CaptureAsync(
            Arg.Is<IReadOnlyList<MigrationItem>>(items => items.Count == 1),
            Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureAsync_CreatesJobAndUpdatesStatus()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems = [];

        await _sut.CaptureAsync();

        await _jobLogger.Received(1).CreateJobAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
        await _jobLogger.Received(1).UpdateJobAsync(
            Arg.Is<MigrationJob>(j => j.Status == JobStatus.Completed),
            Arg.Any<CancellationToken>());
        _session.CurrentJob.Should().NotBeNull();
        _session.CurrentJob!.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_OnFailure_SetsJobStatusToFailed()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems =
        [
            new MigrationItem
            {
                DisplayName = "Fail App",
                IsSelected = true,
                RecommendedTier = MigrationTier.Package,
                ItemType = MigrationItemType.Application
            }
        ];

        _packageMigrator.CaptureAsync(
            Arg.Any<IReadOnlyList<MigrationItem>>(),
            Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Capture failed"));

        var act = () => _sut.CaptureAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();

        _session.CurrentJob!.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task CaptureAsync_OnCancel_SetsJobStatusToCancelled()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems =
        [
            new MigrationItem
            {
                DisplayName = "App",
                IsSelected = true,
                RecommendedTier = MigrationTier.Package,
                ItemType = MigrationItemType.Application
            }
        ];

        _packageMigrator.CaptureAsync(
            Arg.Any<IReadOnlyList<MigrationItem>>(),
            Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new OperationCanceledException());

        var act = () => _sut.CaptureAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();

        _session.CurrentJob!.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task RestoreAsync_CallsPackageAndRegistryMigrators()
    {
        _session.InputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.UserMappings = [new UserMapping { DestinationUsername = "Bill" }];

        await _sut.RestoreAsync();

        await _packageMigrator.Received(1).RestoreAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<UserMapping>>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>());

        await _registryMigrator.Received(1).RestoreAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<UserMapping>>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_CreatesJobAndUpdatesStatus()
    {
        _session.InputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.UserMappings = [];

        await _sut.RestoreAsync();

        await _jobLogger.Received(1).CreateJobAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
        _session.CurrentJob.Should().NotBeNull();
        _session.CurrentJob!.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task RestoreAsync_OnFailure_SetsJobStatusToFailed()
    {
        _session.InputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.UserMappings = [];

        _packageMigrator.RestoreAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<UserMapping>>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Restore failed"));

        var act = () => _sut.RestoreAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();

        _session.CurrentJob!.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task CaptureAsync_UnselectedItems_AreSkipped()
    {
        _session.OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _session.SelectedItems =
        [
            new MigrationItem
            {
                DisplayName = "Skip Me",
                IsSelected = false,
                RecommendedTier = MigrationTier.Package,
                ItemType = MigrationItemType.Application
            }
        ];

        await _sut.CaptureAsync();

        await _packageMigrator.DidNotReceive().CaptureAsync(
            Arg.Any<IReadOnlyList<MigrationItem>>(),
            Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<CancellationToken>());
    }
}
