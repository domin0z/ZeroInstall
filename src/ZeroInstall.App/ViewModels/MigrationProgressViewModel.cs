using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Runs the capture or restore operation, showing real-time progress.
/// </summary>
public partial class MigrationProgressViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly IMigrationCoordinator _coordinator;
    private readonly INavigationService _navigationService;
    private CancellationTokenSource? _cts;

    public override string Title => "Migrate";

    [ObservableProperty]
    private double _overallPercent;

    [ObservableProperty]
    private string _overallPercentFormatted = "0%";

    [ObservableProperty]
    private string _currentItemName = string.Empty;

    [ObservableProperty]
    private string _speedFormatted = string.Empty;

    [ObservableProperty]
    private string _etaFormatted = string.Empty;

    [ObservableProperty]
    private string _statusText = "Preparing...";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _hasErrors;

    public ObservableCollection<MigrationItemProgressViewModel> ItemProgress { get; } = [];

    public MigrationProgressViewModel(
        ISessionState session,
        IMigrationCoordinator coordinator,
        INavigationService navigationService)
    {
        _session = session;
        _coordinator = coordinator;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedTo()
    {
        // Populate item progress list
        var items = _session.Role == MachineRole.Source
            ? _session.SelectedItems.Where(i => i.IsSelected).ToList()
            : [];

        ItemProgress.Clear();
        foreach (var item in items)
        {
            ItemProgress.Add(new MigrationItemProgressViewModel(item));
        }

        await RunMigrationAsync();
    }

    internal async Task RunMigrationAsync()
    {
        _cts = new CancellationTokenSource();
        IsRunning = true;
        IsComplete = false;
        HasErrors = false;

        var transferProgress = new Progress<TransferProgress>(OnTransferProgress);
        var statusProgress = new Progress<string>(status => StatusText = status);

        try
        {
            if (_session.Role == MachineRole.Source)
            {
                StatusText = "Capturing...";
                await _coordinator.CaptureAsync(transferProgress, statusProgress, _cts.Token);
            }
            else
            {
                StatusText = "Restoring...";
                await _coordinator.RestoreAsync(transferProgress, statusProgress, _cts.Token);
            }

            StatusText = "Complete";
            IsComplete = true;
            _navigationService.NavigateTo<JobSummaryViewModel>();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            HasErrors = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
            HasErrors = true;
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsRunning;

    private void OnTransferProgress(TransferProgress p)
    {
        OverallPercent = p.OverallPercentage;
        OverallPercentFormatted = $"{p.OverallPercentage:P0}";
        CurrentItemName = p.CurrentItemName;

        if (p.BytesPerSecond > 0)
            SpeedFormatted = FormatSpeed(p.BytesPerSecond);

        if (p.EstimatedTimeRemaining.HasValue)
            EtaFormatted = $"~{p.EstimatedTimeRemaining.Value:m\\:ss} remaining";

        // Update per-item statuses
        if (p.CurrentItemIndex > 0 && p.CurrentItemIndex <= ItemProgress.Count)
        {
            for (var i = 0; i < p.CurrentItemIndex - 1 && i < ItemProgress.Count; i++)
            {
                if (ItemProgress[i].Status == MigrationItemStatus.Queued)
                    ItemProgress[i].Status = MigrationItemStatus.Completed;
            }

            if (p.CurrentItemIndex - 1 < ItemProgress.Count)
                ItemProgress[p.CurrentItemIndex - 1].Status = MigrationItemStatus.InProgress;
        }
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond / (1024.0 * 1024.0):F1} MB/s";
    }
}
