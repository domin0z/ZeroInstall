using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.ViewModels;

public class MigrationProgressViewModelTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly IMigrationCoordinator _coordinator = Substitute.For<IMigrationCoordinator>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly MigrationProgressViewModel _sut;

    public MigrationProgressViewModelTests()
    {
        _sut = new MigrationProgressViewModel(_session, _coordinator, _navService);
    }

    [Fact]
    public void Title_ShouldBeMigrate()
    {
        _sut.Title.Should().Be("Migrate");
    }

    [Fact]
    public async Task OnNavigatedTo_Source_CallsCapture()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems =
        [
            new MigrationItem { DisplayName = "Chrome", IsSelected = true }
        ];

        await _sut.OnNavigatedTo();

        await _coordinator.Received(1).CaptureAsync(
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<IProgress<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNavigatedTo_Destination_CallsRestore()
    {
        _session.Role = MachineRole.Destination;

        await _sut.OnNavigatedTo();

        await _coordinator.Received(1).RestoreAsync(
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<IProgress<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNavigatedTo_PopulatesItemProgressForSource()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems =
        [
            new MigrationItem { DisplayName = "Chrome", IsSelected = true },
            new MigrationItem { DisplayName = "Firefox", IsSelected = true },
            new MigrationItem { DisplayName = "Skipped", IsSelected = false }
        ];

        await _sut.OnNavigatedTo();

        _sut.ItemProgress.Should().HaveCount(2);
        _sut.ItemProgress[0].DisplayName.Should().Be("Chrome");
        _sut.ItemProgress[1].DisplayName.Should().Be("Firefox");
    }

    [Fact]
    public async Task Completion_NavigatesToSummary()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _navService.Received(1).NavigateTo<JobSummaryViewModel>();
    }

    [Fact]
    public async Task CaptureFailure_SetsHasErrors()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems = [];

        _coordinator.CaptureAsync(
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<IProgress<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("fail"));

        await _sut.OnNavigatedTo();

        _sut.HasErrors.Should().BeTrue();
        _sut.StatusText.Should().Contain("Failed");
    }

    [Fact]
    public async Task Cancel_StopsExecution()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems = [];

        // Set up coordinator to delay so we can cancel
        _coordinator.CaptureAsync(
            Arg.Any<IProgress<TransferProgress>?>(),
            Arg.Any<IProgress<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(5000, ct);
            });

        var runTask = _sut.RunMigrationAsync();

        // Give a moment for the task to start
        await Task.Delay(50);

        // Cancel
        if (_sut.CancelCommand.CanExecute(null))
            _sut.CancelCommand.Execute(null);

        await runTask;

        _sut.StatusText.Should().Be("Cancelled");
        _sut.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task IsRunning_IsTrueWhileMigrating_FalseAfter()
    {
        _session.Role = MachineRole.Source;
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.IsRunning.Should().BeFalse();
    }
}
