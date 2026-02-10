using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Models;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Application settings screen — NAS path, defaults, and preferences.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettings _appSettings;
    private readonly INavigationService _navigationService;

    public override string Title => "Settings";

    [ObservableProperty]
    private string? _nasPath;

    [ObservableProperty]
    private TransportMethod _defaultTransportMethod;

    [ObservableProperty]
    private string _defaultLogLevel = "Information";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(IAppSettings appSettings, INavigationService navigationService)
    {
        _appSettings = appSettings;
        _navigationService = navigationService;
    }

    public override Task OnNavigatedTo()
    {
        var current = _appSettings.Current;
        NasPath = current.NasPath;
        DefaultTransportMethod = current.DefaultTransportMethod;
        DefaultLogLevel = current.DefaultLogLevel;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = new AppSettings
        {
            NasPath = NasPath,
            DefaultTransportMethod = DefaultTransportMethod,
            DefaultLogLevel = DefaultLogLevel
        };

        try
        {
            await _appSettings.SaveAsync(settings);
            StatusMessage = "Settings saved.";
            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.GoBack();
    }
}
