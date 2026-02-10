using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Lists past migration jobs with detail view and report export.
/// </summary>
public partial class JobHistoryViewModel : ViewModelBase
{
    private readonly IJobLogger _jobLogger;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public override string Title => "Job History";

    public ObservableCollection<MigrationJob> Jobs { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportReportCommand))]
    private MigrationJob? _selectedJob;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public JobHistoryViewModel(IJobLogger jobLogger, IDialogService dialogService, INavigationService navigationService)
    {
        _jobLogger = jobLogger;
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedTo()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = string.Empty;
        Jobs.Clear();
        SelectedJob = null;

        try
        {
            var jobs = await _jobLogger.ListJobsAsync();
            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }

            if (Jobs.Count == 0)
            {
                StatusMessage = "No jobs found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading jobs: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedJob))]
    private async Task ExportReportAsync()
    {
        if (SelectedJob is null) return;

        var folder = await _dialogService.BrowseFolderAsync("Select Export Folder");
        if (folder is null) return;

        try
        {
            var report = await _jobLogger.GenerateReportAsync(SelectedJob.JobId);
            await _jobLogger.ExportReportAsync(report, folder);
            StatusMessage = "Report exported successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting report: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    internal bool HasSelectedJob() => SelectedJob is not null;
}
