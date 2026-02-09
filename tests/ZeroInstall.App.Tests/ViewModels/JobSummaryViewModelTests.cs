using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Tests.ViewModels;

public class JobSummaryViewModelTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly IJobLogger _jobLogger = Substitute.For<IJobLogger>();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly JobSummaryViewModel _sut;

    public JobSummaryViewModelTests()
    {
        _sut = new JobSummaryViewModel(_session, _jobLogger, _navService);
    }

    [Fact]
    public void Title_ShouldBeSummary()
    {
        _sut.Title.Should().Be("Summary");
    }

    [Fact]
    public async Task OnNavigatedTo_ComputesCounts()
    {
        _session.CurrentJob = new MigrationJob
        {
            Status = JobStatus.Completed,
            StartedUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedUtc = DateTime.UtcNow,
            Items =
            [
                new MigrationItem { DisplayName = "A", Status = MigrationItemStatus.Completed },
                new MigrationItem { DisplayName = "B", Status = MigrationItemStatus.Completed },
                new MigrationItem { DisplayName = "C", Status = MigrationItemStatus.Failed },
                new MigrationItem { DisplayName = "D", Status = MigrationItemStatus.Warning },
                new MigrationItem { DisplayName = "E", Status = MigrationItemStatus.Skipped },
            ]
        };

        await _sut.OnNavigatedTo();

        _sut.SucceededCount.Should().Be(2);
        _sut.FailedCount.Should().Be(1);
        _sut.WarningCount.Should().Be(1);
        _sut.SkippedCount.Should().Be(1);
        _sut.HasFailures.Should().BeTrue();
        _sut.HasWarnings.Should().BeTrue();
        _sut.ItemResults.Should().HaveCount(5);
    }

    [Fact]
    public async Task OnNavigatedTo_FormatsDuration()
    {
        _session.CurrentJob = new MigrationJob
        {
            Status = JobStatus.Completed,
            StartedUtc = DateTime.UtcNow.AddMinutes(-12).AddSeconds(-34),
            CompletedUtc = DateTime.UtcNow,
            Items = []
        };

        await _sut.OnNavigatedTo();

        _sut.DurationFormatted.Should().Contain("12 min");
    }

    [Fact]
    public async Task OnNavigatedTo_SetsOverallStatus_Completed()
    {
        _session.CurrentJob = new MigrationJob
        {
            Status = JobStatus.Completed,
            StartedUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTime.UtcNow,
            Items = [new MigrationItem { Status = MigrationItemStatus.Completed }]
        };

        await _sut.OnNavigatedTo();

        _sut.OverallStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task OnNavigatedTo_SetsOverallStatus_CompletedWithErrors()
    {
        _session.CurrentJob = new MigrationJob
        {
            Status = JobStatus.Completed,
            StartedUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTime.UtcNow,
            Items =
            [
                new MigrationItem { Status = MigrationItemStatus.Completed },
                new MigrationItem { Status = MigrationItemStatus.Failed }
            ]
        };

        await _sut.OnNavigatedTo();

        _sut.OverallStatus.Should().Be("Completed with errors");
    }

    [Fact]
    public async Task ExportReport_CallsJobLogger()
    {
        var jobId = Guid.NewGuid().ToString("N");
        _session.CurrentJob = new MigrationJob { JobId = jobId, Items = [] };
        _session.OutputPath = Path.Combine(Path.GetTempPath(), "test-report");

        _jobLogger.GenerateReportAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(new JobReport { JobId = jobId });

        await _sut.OnNavigatedTo();
        await _sut.ExportReportCommand.ExecuteAsync(null);

        await _jobLogger.Received(1).GenerateReportAsync(jobId, Arg.Any<CancellationToken>());
        await _jobLogger.Received(1).ExportReportAsync(
            Arg.Any<JobReport>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NewMigration_ResetsSessionAndNavigates()
    {
        _session.OutputPath = @"E:\capture";
        _session.CurrentJob = new MigrationJob();

        _sut.NewMigrationCommand.Execute(null);

        _session.OutputPath.Should().BeEmpty();
        _session.CurrentJob.Should().BeNull();
        _navService.Received(1).NavigateTo<WelcomeViewModel>();
    }
}
