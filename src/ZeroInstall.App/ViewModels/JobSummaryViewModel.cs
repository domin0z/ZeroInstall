using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Displays the final results of a migration job with per-item status.
/// </summary>
public partial class JobSummaryViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly IJobLogger _jobLogger;
    private readonly INavigationService _navigationService;

    public override string Title => "Summary";

    [ObservableProperty]
    private int _succeededCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _skippedCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private string _durationFormatted = string.Empty;

    [ObservableProperty]
    private string _overallStatus = string.Empty;

    [ObservableProperty]
    private bool _hasFailures;

    [ObservableProperty]
    private bool _hasWarnings;

    public ObservableCollection<MigrationItemProgressViewModel> ItemResults { get; } = [];

    public JobSummaryViewModel(
        ISessionState session,
        IJobLogger jobLogger,
        INavigationService navigationService)
    {
        _session = session;
        _jobLogger = jobLogger;
        _navigationService = navigationService;
    }

    public override Task OnNavigatedTo()
    {
        var job = _session.CurrentJob;
        if (job is null) return Task.CompletedTask;

        foreach (var item in job.Items)
        {
            ItemResults.Add(new MigrationItemProgressViewModel(item));
        }

        SucceededCount = job.Items.Count(i => i.Status == MigrationItemStatus.Completed);
        FailedCount = job.Items.Count(i => i.Status == MigrationItemStatus.Failed);
        SkippedCount = job.Items.Count(i => i.Status == MigrationItemStatus.Skipped);
        WarningCount = job.Items.Count(i => i.Status == MigrationItemStatus.Warning);

        HasFailures = FailedCount > 0;
        HasWarnings = WarningCount > 0;

        if (job.Duration.HasValue)
        {
            var d = job.Duration.Value;
            DurationFormatted = d.TotalMinutes >= 1
                ? $"{(int)d.TotalMinutes} min {d.Seconds} sec"
                : $"{d.Seconds} sec";
        }

        OverallStatus = job.Status switch
        {
            JobStatus.Completed when HasFailures => "Completed with errors",
            JobStatus.Completed when HasWarnings => "Completed with warnings",
            JobStatus.Completed => "Completed",
            JobStatus.Failed => "Failed",
            JobStatus.Cancelled => "Cancelled",
            _ => job.Status.ToString()
        };

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        var job = _session.CurrentJob;
        if (job is null) return;

        var report = await _jobLogger.GenerateReportAsync(job.JobId);
        // In production, a save-file dialog would provide the path.
        // For now, export to the output/input path with a standard name.
        var basePath = !string.IsNullOrEmpty(_session.OutputPath)
            ? _session.OutputPath
            : _session.InputPath;

        if (!string.IsNullOrEmpty(basePath))
        {
            var reportPath = System.IO.Path.Combine(basePath, $"report-{job.JobId}.json");
            await _jobLogger.ExportReportAsync(report, reportPath);
        }
    }

    [RelayCommand]
    private void NewMigration()
    {
        _session.Reset();
        _navigationService.NavigateTo<WelcomeViewModel>();
    }
}
