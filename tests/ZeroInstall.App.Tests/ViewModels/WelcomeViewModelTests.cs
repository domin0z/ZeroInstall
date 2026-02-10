using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Tests.ViewModels;

public class WelcomeViewModelTests
{
    private readonly INavigationService _navService;
    private readonly ISessionState _session;
    private readonly WelcomeViewModel _sut;

    public WelcomeViewModelTests()
    {
        _navService = Substitute.For<INavigationService>();
        _session = Substitute.For<ISessionState>();
        _sut = new WelcomeViewModel(_navService, _session);
    }

    [Fact]
    public void Title_IsWelcome()
    {
        _sut.Title.Should().Be("Welcome");
    }

    [Fact]
    public void SelectSourceCommand_SetsRoleToSource()
    {
        _sut.SelectSourceCommand.Execute(null);

        _sut.SelectedRole.Should().Be(MachineRole.Source);
    }

    [Fact]
    public void SelectSourceCommand_NavigatesToDiscoveryViewModel()
    {
        _sut.SelectSourceCommand.Execute(null);

        _navService.Received(1).NavigateTo<DiscoveryViewModel>();
    }

    [Fact]
    public void SelectDestinationCommand_SetsRoleToDestination()
    {
        _sut.SelectDestinationCommand.Execute(null);

        _sut.SelectedRole.Should().Be(MachineRole.Destination);
    }

    [Fact]
    public void SelectDestinationCommand_NavigatesToRestoreConfigViewModel()
    {
        _sut.SelectDestinationCommand.Execute(null);

        _navService.Received(1).NavigateTo<RestoreConfigViewModel>();
    }

    [Fact]
    public void SelectSourceCommand_SetsSessionRole()
    {
        _sut.SelectSourceCommand.Execute(null);

        _session.Received(1).Role = MachineRole.Source;
    }

    [Fact]
    public void SelectDestinationCommand_SetsSessionRole()
    {
        _sut.SelectDestinationCommand.Execute(null);

        _session.Received(1).Role = MachineRole.Destination;
    }

    [Fact]
    public void OpenProfileManagerCommand_NavigatesToProfileList()
    {
        _sut.OpenProfileManagerCommand.Execute(null);

        _navService.Received(1).NavigateTo<ProfileListViewModel>();
    }

    [Fact]
    public void OpenJobHistoryCommand_NavigatesToJobHistory()
    {
        _sut.OpenJobHistoryCommand.Execute(null);

        _navService.Received(1).NavigateTo<JobHistoryViewModel>();
    }

    [Fact]
    public void OpenSettingsCommand_NavigatesToSettings()
    {
        _sut.OpenSettingsCommand.Execute(null);

        _navService.Received(1).NavigateTo<SettingsViewModel>();
    }
}
