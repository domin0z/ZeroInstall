using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Services;

/// <summary>
/// ViewModel-first navigation with back/forward stacks.
/// Resolves view models from DI and calls lifecycle hooks.
/// </summary>
internal sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<ViewModelBase> _backStack = new();
    private readonly Stack<ViewModelBase> _forwardStack = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ViewModelBase? CurrentViewModel { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = (TViewModel)_serviceProvider.GetService(typeof(TViewModel))!;

        if (CurrentViewModel is not null)
        {
            CurrentViewModel.OnNavigatedFrom().GetAwaiter().GetResult();
            _backStack.Push(CurrentViewModel);
        }

        _forwardStack.Clear();
        SetCurrentViewModel(viewModel);
    }

    public void GoBack()
    {
        if (!CanGoBack)
            return;

        if (CurrentViewModel is not null)
        {
            CurrentViewModel.OnNavigatedFrom().GetAwaiter().GetResult();
            _forwardStack.Push(CurrentViewModel);
        }

        var previous = _backStack.Pop();
        SetCurrentViewModel(previous);
    }

    public void GoForward()
    {
        if (!CanGoForward)
            return;

        if (CurrentViewModel is not null)
        {
            CurrentViewModel.OnNavigatedFrom().GetAwaiter().GetResult();
            _backStack.Push(CurrentViewModel);
        }

        var next = _forwardStack.Pop();
        SetCurrentViewModel(next);
    }

    private void SetCurrentViewModel(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
        viewModel.OnNavigatedTo().GetAwaiter().GetResult();
        CurrentViewModelChanged?.Invoke(viewModel);
    }
}
