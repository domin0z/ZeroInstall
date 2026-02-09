using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly INavigationService _navService;
    private readonly MainWindowViewModel _sut;

    public MainWindowViewModelTests()
    {
        _navService = Substitute.For<INavigationService>();
        _sut = new MainWindowViewModel(_navService);
    }

    [Fact]
    public void InitialState_StatusTextIsReady()
    {
        _sut.StatusText.Should().Be("Ready");
    }

    [Fact]
    public void InitialState_StepNamesContainsThreeSteps()
    {
        _sut.StepNames.Should().HaveCount(3);
        _sut.StepNames.Should().ContainInOrder("Welcome", "Discover", "Migrate");
    }

    [Fact]
    public void GoBackCommand_CallsNavigationServiceGoBack()
    {
        _sut.GoBackCommand.Execute(null);

        _navService.Received(1).GoBack();
    }

    [Fact]
    public void GoForwardCommand_CallsNavigationServiceGoForward()
    {
        _sut.GoForwardCommand.Execute(null);

        _navService.Received(1).GoForward();
    }

    [Fact]
    public void CurrentViewModelChanged_UpdatesCurrentViewModel()
    {
        var welcomeVm = Substitute.For<WelcomeViewModel>(Substitute.For<INavigationService>());

        // Trigger the event
        _navService.CurrentViewModelChanged += Raise.Event<Action<ViewModelBase>>(welcomeVm);

        _sut.CurrentViewModel.Should().BeSameAs(welcomeVm);
    }

    [Fact]
    public void CurrentViewModelChanged_UpdatesCanGoBackAndForward()
    {
        _navService.CanGoBack.Returns(true);
        _navService.CanGoForward.Returns(true);
        var vm = new TestStubViewModel();

        _navService.CurrentViewModelChanged += Raise.Event<Action<ViewModelBase>>(vm);

        _sut.CanGoBack.Should().BeTrue();
        _sut.CanGoForward.Should().BeTrue();
    }

    private class TestStubViewModel : ViewModelBase { }
}
