using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App.Services;

/// <summary>
/// ViewModel-first navigation service with back/forward history.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigate to a new view model, resolved from DI.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>
    /// Navigate back to the previous view model.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Navigate forward to the next view model (after going back).
    /// </summary>
    void GoForward();

    /// <summary>
    /// Whether there is a previous view model to navigate back to.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Whether there is a next view model to navigate forward to.
    /// </summary>
    bool CanGoForward { get; }

    /// <summary>
    /// The currently active view model.
    /// </summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>
    /// Raised when the current view model changes.
    /// </summary>
    event Action<ViewModelBase>? CurrentViewModelChanged;
}
