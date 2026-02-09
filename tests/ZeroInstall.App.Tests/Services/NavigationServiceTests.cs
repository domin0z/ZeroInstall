using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Tests.Services;

public class NavigationServiceTests
{
    private readonly NavigationService _sut;
    private readonly IServiceProvider _serviceProvider;

    public NavigationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestViewModelA>();
        services.AddTransient<TestViewModelB>();
        services.AddTransient<TestViewModelC>();
        _serviceProvider = services.BuildServiceProvider();
        _sut = new NavigationService(_serviceProvider);
    }

    [Fact]
    public void InitialState_CurrentViewModelIsNull()
    {
        _sut.CurrentViewModel.Should().BeNull();
        _sut.CanGoBack.Should().BeFalse();
        _sut.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void NavigateTo_SetsCurrentViewModel()
    {
        _sut.NavigateTo<TestViewModelA>();

        _sut.CurrentViewModel.Should().BeOfType<TestViewModelA>();
    }

    [Fact]
    public void NavigateTo_FiresCurrentViewModelChanged()
    {
        ViewModelBase? received = null;
        _sut.CurrentViewModelChanged += vm => received = vm;

        _sut.NavigateTo<TestViewModelA>();

        received.Should().BeOfType<TestViewModelA>();
    }

    [Fact]
    public void NavigateTo_SecondTime_EnablesGoBack()
    {
        _sut.NavigateTo<TestViewModelA>();
        _sut.NavigateTo<TestViewModelB>();

        _sut.CanGoBack.Should().BeTrue();
        _sut.CurrentViewModel.Should().BeOfType<TestViewModelB>();
    }

    [Fact]
    public void GoBack_ReturnsToPreviousViewModel()
    {
        _sut.NavigateTo<TestViewModelA>();
        var first = _sut.CurrentViewModel;
        _sut.NavigateTo<TestViewModelB>();

        _sut.GoBack();

        _sut.CurrentViewModel.Should().BeSameAs(first);
        _sut.CanGoForward.Should().BeTrue();
    }

    [Fact]
    public void GoForward_ReturnsToNextViewModel()
    {
        _sut.NavigateTo<TestViewModelA>();
        _sut.NavigateTo<TestViewModelB>();
        var second = _sut.CurrentViewModel;
        _sut.GoBack();

        _sut.GoForward();

        _sut.CurrentViewModel.Should().BeSameAs(second);
    }

    [Fact]
    public void NavigateTo_ClearsForwardStack()
    {
        _sut.NavigateTo<TestViewModelA>();
        _sut.NavigateTo<TestViewModelB>();
        _sut.GoBack();

        _sut.NavigateTo<TestViewModelC>();

        _sut.CanGoForward.Should().BeFalse();
    }

    [Fact]
    public void GoBack_WhenCannotGoBack_DoesNothing()
    {
        _sut.NavigateTo<TestViewModelA>();
        var current = _sut.CurrentViewModel;

        _sut.GoBack();

        _sut.CurrentViewModel.Should().BeSameAs(current);
    }

    [Fact]
    public void GoForward_WhenCannotGoForward_DoesNothing()
    {
        _sut.NavigateTo<TestViewModelA>();
        var current = _sut.CurrentViewModel;

        _sut.GoForward();

        _sut.CurrentViewModel.Should().BeSameAs(current);
    }

    [Fact]
    public void NavigateTo_CallsOnNavigatedTo()
    {
        _sut.NavigateTo<TestViewModelA>();

        var vm = (TestViewModelA)_sut.CurrentViewModel!;
        vm.NavigatedToCalled.Should().BeTrue();
    }

    [Fact]
    public void NavigateTo_CallsOnNavigatedFromOnPrevious()
    {
        _sut.NavigateTo<TestViewModelA>();
        var first = (TestViewModelA)_sut.CurrentViewModel!;

        _sut.NavigateTo<TestViewModelB>();

        first.NavigatedFromCalled.Should().BeTrue();
    }
}

// Test view models for navigation testing
public class TestViewModelA : ViewModelBase
{
    public bool NavigatedToCalled { get; private set; }
    public bool NavigatedFromCalled { get; private set; }

    public override Task OnNavigatedTo()
    {
        NavigatedToCalled = true;
        return Task.CompletedTask;
    }

    public override Task OnNavigatedFrom()
    {
        NavigatedFromCalled = true;
        return Task.CompletedTask;
    }
}

public class TestViewModelB : ViewModelBase { }
public class TestViewModelC : ViewModelBase { }
