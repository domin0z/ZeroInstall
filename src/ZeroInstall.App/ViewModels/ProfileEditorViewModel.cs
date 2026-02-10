using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Editor for creating or editing a migration profile.
/// </summary>
public partial class ProfileEditorViewModel : ViewModelBase
{
    private readonly IProfileManager _profileManager;
    private readonly INavigationService _navigationService;

    private bool _isNew = true;

    public override string Title => "Profile Editor";

    // Metadata
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    // User Profiles section
    [ObservableProperty]
    private bool _userProfilesEnabled = true;

    [ObservableProperty]
    private bool _userProfilesIncludeAll = true;

    // Applications section
    [ObservableProperty]
    private bool _applicationsEnabled = true;

    [ObservableProperty]
    private MigrationTier _preferredTier = MigrationTier.Package;

    // Browser Data section
    [ObservableProperty]
    private bool _browserDataEnabled = true;

    [ObservableProperty]
    private bool _includeBookmarks = true;

    [ObservableProperty]
    private bool _includeExtensions = true;

    [ObservableProperty]
    private bool _includePasswords;

    // System Settings section
    [ObservableProperty]
    private bool _systemSettingsEnabled = true;

    [ObservableProperty]
    private bool _wifiProfiles = true;

    [ObservableProperty]
    private bool _printers = true;

    [ObservableProperty]
    private bool _mappedDrives = true;

    [ObservableProperty]
    private bool _environmentVariables;

    [ObservableProperty]
    private bool _scheduledTasks;

    [ObservableProperty]
    private bool _credentials;

    [ObservableProperty]
    private bool _certificates;

    [ObservableProperty]
    private bool _defaultApps = true;

    // Transport preferences
    [ObservableProperty]
    private TransportMethod _preferredTransport = TransportMethod.NetworkShare;

    [ObservableProperty]
    private string? _nasPath;

    [ObservableProperty]
    private bool _compression = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ProfileEditorViewModel(IProfileManager profileManager, INavigationService navigationService)
    {
        _profileManager = profileManager;
        _navigationService = navigationService;
    }

    /// <summary>
    /// Loads an existing profile for editing, or starts fresh (null = new).
    /// </summary>
    public void LoadProfile(MigrationProfile? profile)
    {
        if (profile is null)
        {
            _isNew = true;
            return;
        }

        _isNew = false;

        ProfileName = profile.Name;
        Description = profile.Description;
        Author = profile.Author;

        UserProfilesEnabled = profile.Items.UserProfiles.Enabled;
        UserProfilesIncludeAll = profile.Items.UserProfiles.IncludeAll;

        ApplicationsEnabled = profile.Items.Applications.Enabled;
        PreferredTier = profile.Items.Applications.PreferredTier;

        BrowserDataEnabled = profile.Items.BrowserData.Enabled;
        IncludeBookmarks = profile.Items.BrowserData.IncludeBookmarks;
        IncludeExtensions = profile.Items.BrowserData.IncludeExtensions;
        IncludePasswords = profile.Items.BrowserData.IncludePasswords;

        SystemSettingsEnabled = profile.Items.SystemSettings.Enabled;
        WifiProfiles = profile.Items.SystemSettings.WifiProfiles;
        Printers = profile.Items.SystemSettings.Printers;
        MappedDrives = profile.Items.SystemSettings.MappedDrives;
        EnvironmentVariables = profile.Items.SystemSettings.EnvironmentVariables;
        ScheduledTasks = profile.Items.SystemSettings.ScheduledTasks;
        Credentials = profile.Items.SystemSettings.Credentials;
        Certificates = profile.Items.SystemSettings.Certificates;
        DefaultApps = profile.Items.SystemSettings.DefaultApps;

        PreferredTransport = profile.Transport.PreferredMethod;
        NasPath = profile.Transport.NasPath;
        Compression = profile.Transport.Compression;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var profile = new MigrationProfile
        {
            Name = ProfileName,
            Description = Description,
            Author = Author,
            ModifiedUtc = DateTime.UtcNow,
            Items = new ProfileItemSelection
            {
                UserProfiles = new ProfileUserProfileSettings
                {
                    Enabled = UserProfilesEnabled,
                    IncludeAll = UserProfilesIncludeAll
                },
                Applications = new ProfileApplicationSettings
                {
                    Enabled = ApplicationsEnabled,
                    PreferredTier = PreferredTier
                },
                BrowserData = new ProfileBrowserSettings
                {
                    Enabled = BrowserDataEnabled,
                    IncludeBookmarks = IncludeBookmarks,
                    IncludeExtensions = IncludeExtensions,
                    IncludePasswords = IncludePasswords
                },
                SystemSettings = new ProfileSystemSettings
                {
                    Enabled = SystemSettingsEnabled,
                    WifiProfiles = WifiProfiles,
                    Printers = Printers,
                    MappedDrives = MappedDrives,
                    EnvironmentVariables = EnvironmentVariables,
                    ScheduledTasks = ScheduledTasks,
                    Credentials = Credentials,
                    Certificates = Certificates,
                    DefaultApps = DefaultApps
                }
            },
            Transport = new ProfileTransportPreferences
            {
                PreferredMethod = PreferredTransport,
                NasPath = NasPath,
                Compression = Compression
            }
        };

        if (_isNew)
        {
            profile.CreatedUtc = DateTime.UtcNow;
        }

        try
        {
            await _profileManager.SaveLocalProfileAsync(profile);
            StatusMessage = "Profile saved.";
            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.GoBack();
    }

    internal bool CanSave() => !string.IsNullOrWhiteSpace(ProfileName);
}
