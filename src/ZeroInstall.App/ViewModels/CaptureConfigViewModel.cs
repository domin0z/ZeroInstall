using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Configure capture — set output path, transport method, and review summary before starting.
/// </summary>
public partial class CaptureConfigViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    public override string Title => "Configure";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private TransportMethod _selectedTransport;

    [ObservableProperty]
    private int _packageItemCount;

    [ObservableProperty]
    private int _regFileItemCount;

    [ObservableProperty]
    private int _profileItemCount;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    // Transport config — Network Share
    [ObservableProperty]
    private string _networkSharePath = string.Empty;

    [ObservableProperty]
    private string _networkShareUsername = string.Empty;

    [ObservableProperty]
    private string _networkSharePassword = string.Empty;

    // Transport config — Direct WiFi
    [ObservableProperty]
    private int _directWiFiPort = 19850;

    [ObservableProperty]
    private string _directWiFiSharedKey = string.Empty;

    public ObservableCollection<TransportMethod> TransportMethods { get; } =
        [TransportMethod.ExternalStorage, TransportMethod.NetworkShare, TransportMethod.DirectWiFi];

    public CaptureConfigViewModel(ISessionState session, INavigationService navigationService, IDialogService dialogService)
    {
        _session = session;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    public override Task OnNavigatedTo()
    {
        var items = _session.SelectedItems.Where(i => i.IsSelected).ToList();

        PackageItemCount = items.Count(i => i.EffectiveTier == MigrationTier.Package);
        RegFileItemCount = items.Count(i => i.EffectiveTier == MigrationTier.RegistryFile);
        ProfileItemCount = items.Count(i =>
            i.ItemType == MigrationItemType.UserProfile
            || i.ItemType == MigrationItemType.SystemSetting
            || i.ItemType == MigrationItemType.BrowserData);

        var totalBytes = items.Sum(i => i.EstimatedSizeBytes);
        TotalSizeFormatted = FormatBytes(totalBytes);

        // Restore transport config from session
        NetworkSharePath = _session.NetworkSharePath;
        NetworkShareUsername = _session.NetworkShareUsername;
        NetworkSharePassword = _session.NetworkSharePassword;
        DirectWiFiPort = _session.DirectWiFiPort;
        DirectWiFiSharedKey = _session.DirectWiFiSharedKey;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var path = await _dialogService.BrowseFolderAsync("Select Output Folder", OutputPath);
        if (path is not null)
        {
            OutputPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private void StartCapture()
    {
        _session.OutputPath = OutputPath;
        _session.TransportMethod = SelectedTransport;
        _session.NetworkSharePath = NetworkSharePath;
        _session.NetworkShareUsername = NetworkShareUsername;
        _session.NetworkSharePassword = NetworkSharePassword;
        _session.DirectWiFiPort = DirectWiFiPort;
        _session.DirectWiFiSharedKey = DirectWiFiSharedKey;
        _navigationService.NavigateTo<MigrationProgressViewModel>();
    }

    internal bool CanStartCapture() => !string.IsNullOrWhiteSpace(OutputPath);

    internal static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0
            ? $"{size:F0} {units[order]}"
            : $"{size:F1} {units[order]}";
    }
}
