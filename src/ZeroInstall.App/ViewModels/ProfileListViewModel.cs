using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Lists local and NAS migration profiles with CRUD actions.
/// </summary>
public partial class ProfileListViewModel : ViewModelBase
{
    private readonly IProfileManager _profileManager;
    private readonly INavigationService _navigationService;
    private readonly ISessionState _session;
    private readonly IAppSettings _appSettings;

    public override string Title => "Profiles";

    public ObservableCollection<MigrationProfile> LocalProfiles { get; } = [];
    public ObservableCollection<MigrationProfile> NasProfiles { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadProfileCommand))]
    private MigrationProfile? _selectedProfile;

    [ObservableProperty]
    private bool _hasNasPath;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ProfileListViewModel(
        IProfileManager profileManager,
        INavigationService navigationService,
        ISessionState session,
        IAppSettings appSettings)
    {
        _profileManager = profileManager;
        _navigationService = navigationService;
        _session = session;
        _appSettings = appSettings;
    }

    public override async Task OnNavigatedTo()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = string.Empty;
        LocalProfiles.Clear();
        NasProfiles.Clear();

        try
        {
            var localProfiles = await _profileManager.ListLocalProfilesAsync();
            foreach (var profile in localProfiles)
            {
                LocalProfiles.Add(profile);
            }

            HasNasPath = !string.IsNullOrEmpty(_appSettings.Current.NasPath);
            if (HasNasPath)
            {
                var nasProfiles = await _profileManager.ListNasProfilesAsync();
                foreach (var profile in nasProfiles)
                {
                    NasProfiles.Add(profile);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading profiles: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CreateNew()
    {
        _navigationService.NavigateTo<ProfileEditorViewModel>();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void EditProfile()
    {
        if (SelectedProfile is null) return;

        _navigationService.NavigateTo<ProfileEditorViewModel>();
        if (_navigationService.CurrentViewModel is ProfileEditorViewModel editor)
        {
            editor.LoadProfile(SelectedProfile);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null) return;

        try
        {
            await _profileManager.DeleteLocalProfileAsync(SelectedProfile.Name);
            LocalProfiles.Remove(SelectedProfile);
            SelectedProfile = null;
            StatusMessage = "Profile deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting profile: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void LoadProfile()
    {
        if (SelectedProfile is null) return;

        // Apply profile transport preferences to session
        _session.TransportMethod = SelectedProfile.Transport.PreferredMethod;
        StatusMessage = $"Profile '{SelectedProfile.Name}' loaded.";
        _navigationService.NavigateTo<WelcomeViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    internal bool HasSelectedProfile() => SelectedProfile is not null;
}
