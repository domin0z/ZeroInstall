using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Base class for all view models. Provides lifecycle hooks and a display title.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Display title for step indicators and breadcrumbs.
    /// </summary>
    public virtual string Title => GetType().Name.Replace("ViewModel", "");

    /// <summary>
    /// Called when this view model becomes the active view.
    /// </summary>
    public virtual Task OnNavigatedTo() => Task.CompletedTask;

    /// <summary>
    /// Called when this view model is navigated away from.
    /// </summary>
    public virtual Task OnNavigatedFrom() => Task.CompletedTask;
}
