using NSubstitute;
using ZeroInstall.App.Models;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly IAppSettings _appSettings = Substitute.For<IAppSettings>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly SettingsViewModel _sut;

    public SettingsViewModelTests()
    {
        _appSettings.Current.Returns(new AppSettings());
        _sut = new SettingsViewModel(_appSettings, _navService);
    }

    [Fact]
    public void Title_ShouldBeSettings()
    {
        _sut.Title.Should().Be("Settings");
    }

    [Fact]
    public async Task OnNavigatedTo_LoadsCurrentSettings()
    {
        _appSettings.Current.Returns(new AppSettings
        {
            NasPath = @"\\nas\share",
            DefaultTransportMethod = TransportMethod.NetworkShare,
            DefaultLogLevel = "Warning"
        });

        await _sut.OnNavigatedTo();

        _sut.NasPath.Should().Be(@"\\nas\share");
        _sut.DefaultTransportMethod.Should().Be(TransportMethod.NetworkShare);
        _sut.DefaultLogLevel.Should().Be("Warning");
    }

    [Fact]
    public async Task Save_CallsAppSettingsAndNavigatesBack()
    {
        _sut.NasPath = @"\\nas\new";
        _sut.DefaultTransportMethod = TransportMethod.DirectWiFi;
        _sut.DefaultLogLevel = "Error";

        await _sut.SaveCommand.ExecuteAsync(null);

        await _appSettings.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s =>
                s.NasPath == @"\\nas\new" &&
                s.DefaultTransportMethod == TransportMethod.DirectWiFi &&
                s.DefaultLogLevel == "Error"),
            Arg.Any<CancellationToken>());
        _navService.Received(1).GoBack();
    }

    [Fact]
    public void Cancel_NavigatesBack()
    {
        _sut.CancelCommand.Execute(null);

        _navService.Received(1).GoBack();
    }

    [Fact]
    public async Task OnNavigatedTo_WithDefaults_LoadsDefaults()
    {
        _appSettings.Current.Returns(new AppSettings());

        await _sut.OnNavigatedTo();

        _sut.NasPath.Should().BeNull();
        _sut.DefaultTransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.DefaultLogLevel.Should().Be("Information");
    }

    [Fact]
    public async Task Save_WithNullNasPath_SavesNull()
    {
        _sut.NasPath = null;

        await _sut.SaveCommand.ExecuteAsync(null);

        await _appSettings.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s.NasPath == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_OnError_SetsStatusMessage()
    {
        _appSettings.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Disk full"));

        await _sut.SaveCommand.ExecuteAsync(null);

        _sut.StatusMessage.Should().Contain("Disk full");
        _navService.DidNotReceive().GoBack();
    }

    [Fact]
    public async Task Save_OnSuccess_SetsStatusMessage()
    {
        _sut.NasPath = @"\\test";

        await _sut.SaveCommand.ExecuteAsync(null);

        _sut.StatusMessage.Should().Contain("saved");
    }
}
