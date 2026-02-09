using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Discovery screen — scans the source machine and presents a grouped checklist of items to migrate.
/// </summary>
public partial class DiscoveryViewModel : ViewModelBase
{
    private readonly IDiscoveryService _discoveryService;

    public override string Title => "Discover";

    public ObservableCollection<MigrationItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Ready to scan";

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _selectedSizeBytes;

    [ObservableProperty]
    private string _selectedSizeFormatted = "0 B";

    public DiscoveryViewModel(IDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    public override async Task OnNavigatedTo()
    {
        if (Items.Count == 0)
        {
            await ScanAsync();
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanStatus = "Scanning...";
        Items.Clear();

        try
        {
            var progress = new Progress<string>(status => ScanStatus = status);
            var items = await _discoveryService.DiscoverAllAsync(progress);

            foreach (var item in items)
            {
                Items.Add(new MigrationItemViewModel(item, UpdateSummary));
            }

            ScanStatus = $"Scan complete — {Items.Count} items found";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            UpdateSummary();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var item in Items)
            item.IsSelected = false;
    }

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        // Future: navigate to capture configuration
    }

    private bool CanProceed() => SelectedCount > 0;

    internal void UpdateSummary()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        SelectedCount = selected.Count;
        SelectedSizeBytes = selected.Sum(i => i.EstimatedSizeBytes);
        SelectedSizeFormatted = FormatBytes(SelectedSizeBytes);
        ProceedCommand.NotifyCanExecuteChanged();
    }

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
