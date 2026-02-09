using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Configure restore — select capture location, review source info, and map users.
/// </summary>
public partial class RestoreConfigViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly INavigationService _navigationService;

    public override string Title => "Restore";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCaptureCommand))]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private bool _hasValidCapture;

    [ObservableProperty]
    private string _captureInfo = string.Empty;

    public ObservableCollection<UserMappingEntryViewModel> UserMappings { get; } = [];

    public RestoreConfigViewModel(ISessionState session, INavigationService navigationService)
    {
        _session = session;
        _navigationService = navigationService;
    }

    public override Task OnNavigatedTo()
    {
        if (!string.IsNullOrEmpty(_session.InputPath))
        {
            InputPath = _session.InputPath;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void BrowseInput()
    {
        // In production this opens a folder browser dialog via a service.
    }

    [RelayCommand(CanExecute = nameof(CanLoadCapture))]
    private async Task LoadCaptureAsync()
    {
        HasValidCapture = false;
        CaptureInfo = string.Empty;
        UserMappings.Clear();

        var profileSettingsDir = Path.Combine(InputPath, "profile-settings");
        var manifestPath = Path.Combine(profileSettingsDir, "profile-settings-manifest.json");

        if (!File.Exists(manifestPath))
        {
            CaptureInfo = "No valid capture found at this location.";
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ProfileSettingsManifest>(json);

            if (manifest is null)
            {
                CaptureInfo = "Could not read capture manifest.";
                return;
            }

            CaptureInfo = $"Captured on {manifest.CapturedUtc:yyyy-MM-dd HH:mm}";
            HasValidCapture = true;

            // Populate user mappings — look for profile subdirs
            var profilesDir = Path.Combine(profileSettingsDir, "profiles");
            if (Directory.Exists(profilesDir))
            {
                foreach (var userDir in Directory.GetDirectories(profilesDir))
                {
                    var username = Path.GetFileName(userDir);
                    var mapping = new UserMapping
                    {
                        SourceUser = new UserProfile
                        {
                            Username = username,
                            ProfilePath = $@"C:\Users\{username}"
                        },
                        DestinationUsername = username,
                        DestinationProfilePath = $@"C:\Users\{username}"
                    };
                    UserMappings.Add(new UserMappingEntryViewModel(mapping));
                }
            }

            StartRestoreCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            CaptureInfo = $"Error reading capture: {ex.Message}";
        }
    }

    private bool CanLoadCapture() => !string.IsNullOrWhiteSpace(InputPath);

    [RelayCommand(CanExecute = nameof(CanStartRestore))]
    private void StartRestore()
    {
        _session.InputPath = InputPath;
        _session.UserMappings = UserMappings.Select(vm => vm.Model).ToList();
        _navigationService.NavigateTo<MigrationProgressViewModel>();
    }

    internal bool CanStartRestore() => HasValidCapture;
}
