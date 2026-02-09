using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Tests.ViewModels;

public class WelcomeViewModelTests
{
    private readonly INavigationService _navService;
    private readonly WelcomeViewModel _sut;

    public WelcomeViewModelTests()
    {
        _navService = Substitute.For<INavigationService>();
        _sut = new WelcomeViewModel(_navService);
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
}
