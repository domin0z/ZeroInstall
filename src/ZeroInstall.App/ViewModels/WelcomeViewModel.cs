using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Welcome screen — technician selects whether this machine is the Source or Destination.
/// </summary>
public partial class WelcomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public override string Title => "Welcome";

    /// <summary>
    /// The selected machine role, or null if not yet selected.
    /// </summary>
    public MachineRole? SelectedRole { get; private set; }

    public WelcomeViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void SelectSource()
    {
        SelectedRole = MachineRole.Source;
        _navigationService.NavigateTo<DiscoveryViewModel>();
    }

    [RelayCommand]
    private void SelectDestination()
    {
        SelectedRole = MachineRole.Destination;
        // Future: navigate to restore/destination view
    }
}

/// <summary>
/// Whether this machine is the migration source (old PC) or destination (new PC).
/// </summary>
public enum MachineRole
{
    Source,
    Destination
}
