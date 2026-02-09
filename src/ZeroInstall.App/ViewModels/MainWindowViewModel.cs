using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// View model for the main application shell. Wraps navigation and step tracking.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    public IReadOnlyList<string> StepNames { get; } = ["Welcome", "Discover", "Migrate"];

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += HandleNavigationChanged;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private void GoForward()
    {
        _navigationService.GoForward();
    }

    private void HandleNavigationChanged(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
        CanGoBack = _navigationService.CanGoBack;
        CanGoForward = _navigationService.CanGoForward;

        // Update step index based on view model type
        CurrentStepIndex = viewModel switch
        {
            WelcomeViewModel => 0,
            DiscoveryViewModel => 1,
            _ => CurrentStepIndex
        };
    }
}
