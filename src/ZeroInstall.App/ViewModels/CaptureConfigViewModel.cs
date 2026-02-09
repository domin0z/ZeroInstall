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

    public ObservableCollection<TransportMethod> TransportMethods { get; } =
        [TransportMethod.ExternalStorage, TransportMethod.NetworkShare, TransportMethod.DirectWiFi];

    public CaptureConfigViewModel(ISessionState session, INavigationService navigationService)
    {
        _session = session;
        _navigationService = navigationService;
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

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        // In production this opens a folder browser dialog via a service.
        // For testability, the path is set directly.
    }

    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private void StartCapture()
    {
        _session.OutputPath = OutputPath;
        _session.TransportMethod = SelectedTransport;
        _navigationService.NavigateTo<MigrationProgressViewModel>();
    }

    internal bool CanStartCapture() => !string.IsNullOrWhiteSpace(OutputPath);

    private static string FormatBytes(long bytes)
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
