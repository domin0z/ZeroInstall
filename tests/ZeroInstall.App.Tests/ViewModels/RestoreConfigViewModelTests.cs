using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Tests.ViewModels;

public class RestoreConfigViewModelTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly RestoreConfigViewModel _sut;

    public RestoreConfigViewModelTests()
    {
        _sut = new RestoreConfigViewModel(_session, _navService, _dialogService);
    }

    [Fact]
    public void Title_ShouldBeRestore()
    {
        _sut.Title.Should().Be("Restore");
    }

    [Fact]
    public async Task OnNavigatedTo_WithSessionInputPath_SetsInputPath()
    {
        _session.InputPath = @"E:\restore";

        await _sut.OnNavigatedTo();

        _sut.InputPath.Should().Be(@"E:\restore");
    }

    [Fact]
    public void CanLoadCapture_IsFalse_WhenInputPathEmpty()
    {
        _sut.InputPath = "";
        _sut.LoadCaptureCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadCapture_WithInvalidPath_ShowsError()
    {
        _sut.InputPath = @"C:\nonexistent\path\1234";

        await _sut.LoadCaptureCommand.ExecuteAsync(null);

        _sut.HasValidCapture.Should().BeFalse();
        _sut.CaptureInfo.Should().Contain("No valid capture");
    }

    [Fact]
    public void CanStartRestore_IsFalse_WhenNoValidCapture()
    {
        _sut.CanStartRestore().Should().BeFalse();
    }

    [Fact]
    public async Task LoadCapture_WithValidManifest_SetsHasValidCapture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var profileSettingsDir = Path.Combine(tempDir, "profile-settings");
        var profilesDir = Path.Combine(profileSettingsDir, "profiles", "TestUser");
        Directory.CreateDirectory(profilesDir);

        var manifestPath = Path.Combine(profileSettingsDir, "profile-settings-manifest.json");
        File.WriteAllText(manifestPath, """
        {
            "CapturedUtc": "2026-01-15T10:00:00Z",
            "HasProfiles": true,
            "HasBrowserData": false,
            "HasEmailData": false,
            "HasSystemSettings": false
        }
        """);

        try
        {
            _sut.InputPath = tempDir;
            await _sut.LoadCaptureCommand.ExecuteAsync(null);

            _sut.HasValidCapture.Should().BeTrue();
            _sut.CaptureInfo.Should().Contain("2026");
            _sut.UserMappings.Should().ContainSingle()
                .Which.SourceUsername.Should().Be("TestUser");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task BrowseInput_WhenDialogReturnsPath_SetsInputPath()
    {
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(@"E:\selected");

        await _sut.BrowseInputCommand.ExecuteAsync(null);

        _sut.InputPath.Should().Be(@"E:\selected");
    }

    [Fact]
    public async Task BrowseInput_WhenDialogCancelled_DoesNotChangeInputPath()
    {
        _sut.InputPath = @"E:\original";
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((string?)null);

        await _sut.BrowseInputCommand.ExecuteAsync(null);

        _sut.InputPath.Should().Be(@"E:\original");
    }

    [Fact]
    public async Task BrowseInput_PassesCurrentInputPathAsInitial()
    {
        _sut.InputPath = @"E:\current";
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((string?)null);

        await _sut.BrowseInputCommand.ExecuteAsync(null);

        await _dialogService.Received(1).BrowseFolderAsync(
            Arg.Any<string?>(),
            Arg.Is<string?>(s => s == @"E:\current"));
    }

    #region Bluetooth

    [Fact]
    public void BluetoothDefaultValues_AreCorrect()
    {
        _sut.BluetoothDeviceName.Should().BeEmpty();
        _sut.BluetoothDeviceAddress.Should().Be(0UL);
        _sut.BluetoothIsServer.Should().BeTrue(); // Restore defaults to server mode
        _sut.BluetoothIsScanning.Should().BeFalse();
        _sut.BluetoothIsConnected.Should().BeFalse();
        _sut.BluetoothConnectionStatus.Should().BeEmpty();
        _sut.BluetoothSpeedWarning.Should().BeEmpty();
    }

    [Fact]
    public void BluetoothDiscoveredDevices_DefaultsEmpty()
    {
        _sut.BluetoothDiscoveredDevices.Should().BeEmpty();
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresBluetoothConfigFromSession()
    {
        _session.BluetoothDeviceName = "SourcePC";
        _session.BluetoothDeviceAddress = 0xDEADBEEF;
        _session.BluetoothIsServer = false;

        await _sut.OnNavigatedTo();

        _sut.BluetoothDeviceName.Should().Be("SourcePC");
        _sut.BluetoothDeviceAddress.Should().Be(0xDEADBEEF);
        _sut.BluetoothIsServer.Should().BeFalse();
    }

    [Fact]
    public void BluetoothIsServer_DefaultsTrue_ForRestore()
    {
        // Restore side should default to server (listening) mode
        _sut.BluetoothIsServer.Should().BeTrue();
    }

    #endregion
}
