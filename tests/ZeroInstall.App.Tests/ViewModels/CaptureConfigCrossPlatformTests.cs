using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.ViewModels;

public class CaptureConfigCrossPlatformTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ICrossPlatformDiscoveryService _crossPlatform = Substitute.For<ICrossPlatformDiscoveryService>();

    private CaptureConfigViewModel CreateVm() =>
        new(_session, _navService, _dialogService, crossPlatformDiscovery: _crossPlatform);

    [Fact]
    public void SourcePath_DefaultIsEmpty()
    {
        var vm = CreateVm();

        vm.SourcePath.Should().BeEmpty();
    }

    [Fact]
    public void IsExternalSource_IsFalse_WhenSourcePathEmpty()
    {
        var vm = CreateVm();

        vm.IsExternalSource.Should().BeFalse();
    }

    [Fact]
    public void IsExternalSource_IsTrue_WhenSourcePathSet()
    {
        var vm = CreateVm();
        vm.SourcePath = @"E:\";

        vm.IsExternalSource.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseSource_SetsPath()
    {
        _dialogService.BrowseFolderAsync("Select Source Drive", "")
            .Returns(Task.FromResult<string?>(@"E:\"));
        _crossPlatform.DetectSourcePlatformAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SourcePlatform.Unknown));

        var vm = CreateVm();
        await vm.BrowseSourceCommand.ExecuteAsync(null);

        vm.SourcePath.Should().Be(@"E:\");
    }

    [Fact]
    public async Task DetectPlatform_MacOs_ShowsBanner()
    {
        _crossPlatform.DetectSourcePlatformAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SourcePlatform.MacOs));
        _crossPlatform.DiscoverAllAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CrossPlatformDiscoveryResult
            {
                Platform = SourcePlatform.MacOs,
                OsVersion = "14.2"
            }));

        var vm = CreateVm();
        vm.SourcePath = @"E:\";
        await vm.DetectPlatformCommand.ExecuteAsync(null);

        vm.ShowPlatformBanner.Should().BeTrue();
        vm.DetectedPlatformDisplay.Should().Contain("macOS");
        vm.DetectedPlatformDisplay.Should().Contain("14.2");
    }

    [Fact]
    public async Task DetectPlatform_MacOs_ShowsCrossPlatformWarning()
    {
        _crossPlatform.DetectSourcePlatformAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SourcePlatform.MacOs));
        _crossPlatform.DiscoverAllAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CrossPlatformDiscoveryResult { Platform = SourcePlatform.MacOs }));

        var vm = CreateVm();
        vm.SourcePath = @"E:\";
        await vm.DetectPlatformCommand.ExecuteAsync(null);

        vm.ShowCrossPlatformWarning.Should().BeTrue();
        vm.CrossPlatformWarning.Should().Contain("Registry capture");
    }

    [Fact]
    public async Task DetectPlatform_Linux_ShowsBanner()
    {
        _crossPlatform.DetectSourcePlatformAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SourcePlatform.Linux));
        _crossPlatform.DiscoverAllAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CrossPlatformDiscoveryResult
            {
                Platform = SourcePlatform.Linux,
                OsVersion = "Ubuntu 22.04"
            }));

        var vm = CreateVm();
        vm.SourcePath = @"E:\";
        await vm.DetectPlatformCommand.ExecuteAsync(null);

        vm.ShowPlatformBanner.Should().BeTrue();
        vm.DetectedPlatformDisplay.Should().Contain("Linux");
    }

    [Fact]
    public async Task DetectPlatform_Windows_HidesWarning()
    {
        _crossPlatform.DetectSourcePlatformAsync(@"E:\", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SourcePlatform.Windows));

        var vm = CreateVm();
        vm.SourcePath = @"E:\";
        await vm.DetectPlatformCommand.ExecuteAsync(null);

        vm.ShowPlatformBanner.Should().BeFalse();
        vm.ShowCrossPlatformWarning.Should().BeFalse();
    }

    [Fact]
    public async Task DetectPlatform_EmptyPath_HidesBanner()
    {
        var vm = CreateVm();
        vm.SourcePath = string.Empty;
        await vm.DetectPlatformCommand.ExecuteAsync(null);

        vm.ShowPlatformBanner.Should().BeFalse();
    }

    [Fact]
    public void SessionState_SourcePath_DefaultEmpty()
    {
        var state = new SessionState();

        state.SourcePath.Should().BeEmpty();
    }

    [Fact]
    public void SessionState_Reset_ClearsSourcePath()
    {
        var state = new SessionState();
        state.SourcePath = @"E:\";

        state.Reset();

        state.SourcePath.Should().BeEmpty();
    }
}
