using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.ViewModels;

public class JobHistoryViewModelTests
{
    private readonly IJobLogger _jobLogger = Substitute.For<IJobLogger>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly JobHistoryViewModel _sut;

    public JobHistoryViewModelTests()
    {
        _sut = new JobHistoryViewModel(_jobLogger, _dialogService, _navService);
    }

    [Fact]
    public void Title_ShouldBeJobHistory()
    {
        _sut.Title.Should().Be("Job History");
    }

    [Fact]
    public async Task OnNavigatedTo_LoadsJobs()
    {
        var jobs = new List<MigrationJob>
        {
            new() { SourceHostname = "PC1", DestinationHostname = "PC2" },
            new() { SourceHostname = "PC3", DestinationHostname = "PC4" }
        };
        _jobLogger.ListJobsAsync(Arg.Any<JobStatus?>(), Arg.Any<CancellationToken>())
            .Returns(jobs.AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.Jobs.Should().HaveCount(2);
    }

    [Fact]
    public async Task Refresh_ClearsAndReloads()
    {
        _sut.Jobs.Add(new MigrationJob());
        _jobLogger.ListJobsAsync(Arg.Any<JobStatus?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MigrationJob>().AsReadOnly());

        await _sut.RefreshCommand.ExecuteAsync(null);

        _sut.Jobs.Should().BeEmpty();
        _sut.StatusMessage.Should().Be("No jobs found.");
    }

    [Fact]
    public async Task ExportReport_WithSelectedJob_ExportsToFolder()
    {
        var job = new MigrationJob { JobId = "test123" };
        _sut.SelectedJob = job;
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(@"E:\exports");
        var report = new JobReport { JobId = "test123" };
        _jobLogger.GenerateReportAsync("test123", Arg.Any<CancellationToken>())
            .Returns(report);

        await _sut.ExportReportCommand.ExecuteAsync(null);

        await _jobLogger.Received(1).ExportReportAsync(report, @"E:\exports", Arg.Any<CancellationToken>());
        _sut.StatusMessage.Should().Contain("exported");
    }

    [Fact]
    public async Task ExportReport_WhenCancelled_DoesNotExport()
    {
        var job = new MigrationJob { JobId = "test" };
        _sut.SelectedJob = job;
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((string?)null);

        await _sut.ExportReportCommand.ExecuteAsync(null);

        await _jobLogger.DidNotReceive().GenerateReportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNavigatedTo_WithEmptyList_SetsStatusMessage()
    {
        _jobLogger.ListJobsAsync(Arg.Any<JobStatus?>(), Arg.Any<CancellationToken>())
            .Returns(new List<MigrationJob>().AsReadOnly());

        await _sut.OnNavigatedTo();

        _sut.Jobs.Should().BeEmpty();
        _sut.StatusMessage.Should().Be("No jobs found.");
    }

    [Fact]
    public void SelectedJob_TriggersCanExecuteChange()
    {
        _sut.HasSelectedJob().Should().BeFalse();

        _sut.SelectedJob = new MigrationJob();

        _sut.HasSelectedJob().Should().BeTrue();
    }

    [Fact]
    public void GoBack_CallsNavigationGoBack()
    {
        _sut.GoBackCommand.Execute(null);

        _navService.Received(1).GoBack();
    }
}
