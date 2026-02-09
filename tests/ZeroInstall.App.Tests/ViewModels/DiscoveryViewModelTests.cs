using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.ViewModels;

public class DiscoveryViewModelTests
{
    private readonly IDiscoveryService _discoveryService;
    private readonly ISessionState _session;
    private readonly INavigationService _navService;
    private readonly DiscoveryViewModel _sut;

    public DiscoveryViewModelTests()
    {
        _discoveryService = Substitute.For<IDiscoveryService>();
        _session = Substitute.For<ISessionState>();
        _navService = Substitute.For<INavigationService>();
        _sut = new DiscoveryViewModel(_discoveryService, _session, _navService);
    }

    [Fact]
    public void Title_IsDiscover()
    {
        _sut.Title.Should().Be("Discover");
    }

    [Fact]
    public async Task ScanCommand_PopulatesItems()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "Firefox", ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 },
            new() { DisplayName = "Chrome", ItemType = MigrationItemType.Application, EstimatedSizeBytes = 2048 }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());

        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.Items.Should().HaveCount(2);
        _sut.Items[0].DisplayName.Should().Be("Firefox");
        _sut.Items[1].DisplayName.Should().Be("Chrome");
    }

    [Fact]
    public async Task ScanCommand_UpdatesSummary()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "App1", IsSelected = true, EstimatedSizeBytes = 1_073_741_824 },
            new() { DisplayName = "App2", IsSelected = true, EstimatedSizeBytes = 536_870_912 }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());

        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.SelectedCount.Should().Be(2);
        _sut.SelectedSizeBytes.Should().Be(1_610_612_736);
        _sut.SelectedSizeFormatted.Should().Be("1.5 GB");
    }

    [Fact]
    public async Task ScanCommand_SetsScanStatusToComplete()
    {
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MigrationItem>().AsReadOnly());

        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.ScanStatus.Should().Contain("complete");
        _sut.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task ScanCommand_WhenDiscoveryFails_ShowsError()
    {
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<MigrationItem>>(_ => throw new InvalidOperationException("Access denied"));

        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.ScanStatus.Should().Contain("failed");
        _sut.ScanStatus.Should().Contain("Access denied");
    }

    [Fact]
    public async Task SelectAllCommand_SelectsAllItems()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "App1", IsSelected = false },
            new() { DisplayName = "App2", IsSelected = false }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.SelectAllCommand.Execute(null);

        _sut.Items.Should().AllSatisfy(i => i.IsSelected.Should().BeTrue());
    }

    [Fact]
    public async Task SelectNoneCommand_DeselectsAllItems()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "App1", IsSelected = true },
            new() { DisplayName = "App2", IsSelected = true }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.SelectNoneCommand.Execute(null);

        _sut.Items.Should().AllSatisfy(i => i.IsSelected.Should().BeFalse());
        _sut.SelectedCount.Should().Be(0);
    }

    [Fact]
    public async Task ProceedCommand_DisabledWhenNothingSelected()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "App1", IsSelected = false }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.ProceedCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task ProceedCommand_EnabledWhenItemsSelected()
    {
        var items = new List<MigrationItem>
        {
            new() { DisplayName = "App1", IsSelected = true }
        };
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        await _sut.ScanCommand.ExecuteAsync(null);

        _sut.ProceedCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task OnNavigatedTo_AutoStartsScan()
    {
        _discoveryService.DiscoverAllAsync(Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MigrationItem> { new() { DisplayName = "Test" } }.AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.Items.Should().HaveCount(1);
    }
}
