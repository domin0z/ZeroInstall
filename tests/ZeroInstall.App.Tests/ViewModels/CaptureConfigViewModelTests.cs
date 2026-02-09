using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.ViewModels;

public class CaptureConfigViewModelTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly CaptureConfigViewModel _sut;

    public CaptureConfigViewModelTests()
    {
        _sut = new CaptureConfigViewModel(_session, _navService);
    }

    [Fact]
    public void Title_ShouldBeConfigure()
    {
        _sut.Title.Should().Be("Configure");
    }

    [Fact]
    public async Task OnNavigatedTo_ComputesSummaryStats()
    {
        _session.SelectedItems =
        [
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 100 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 200 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.RegistryFile, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 50 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.UserProfile, EstimatedSizeBytes = 1024 * 1024 * 500 },
            new MigrationItem { IsSelected = false, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 999 },
        ];

        await _sut.OnNavigatedTo();

        _sut.PackageItemCount.Should().Be(3); // 2 apps + 1 UserProfile with Package tier
        _sut.RegFileItemCount.Should().Be(1);
        _sut.ProfileItemCount.Should().Be(1);
        _sut.TotalSizeFormatted.Should().NotBe("0 B");
    }

    [Fact]
    public void CanStartCapture_IsFalse_WhenOutputPathEmpty()
    {
        _sut.OutputPath = "";
        _sut.CanStartCapture().Should().BeFalse();
    }

    [Fact]
    public void CanStartCapture_IsTrue_WhenOutputPathSet()
    {
        _sut.OutputPath = @"E:\capture";
        _sut.CanStartCapture().Should().BeTrue();
    }

    [Fact]
    public void StartCapture_SavesToSessionAndNavigates()
    {
        _sut.OutputPath = @"E:\capture";
        _sut.SelectedTransport = TransportMethod.NetworkShare;

        _sut.StartCaptureCommand.Execute(null);

        _session.OutputPath.Should().Be(@"E:\capture");
        _session.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        _navService.Received(1).NavigateTo<MigrationProgressViewModel>();
    }

    [Fact]
    public async Task OnNavigatedTo_WithNoItems_ShowsZeroCounts()
    {
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.PackageItemCount.Should().Be(0);
        _sut.RegFileItemCount.Should().Be(0);
        _sut.ProfileItemCount.Should().Be(0);
        _sut.TotalSizeFormatted.Should().Be("0 B");
    }
}
