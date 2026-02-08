using Microsoft.Extensions.Logging;
using NSubstitute;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Tests.Services;

public class JsonJobLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonJobLogger _logger;

    public JsonJobLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = new JsonJobLogger(_tempDir, Substitute.For<ILogger<JsonJobLogger>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task CreateJobAsync_PersistsJobToFile()
    {
        var job = CreateTestJob();

        var created = await _logger.CreateJobAsync(job);

        created.JobId.Should().Be(job.JobId);
        var filePath = Path.Combine(_tempDir, "jobs", $"{job.JobId}.json");
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task GetJobAsync_ReturnsPersistedJob()
    {
        var job = CreateTestJob();
        await _logger.CreateJobAsync(job);

        var retrieved = await _logger.GetJobAsync(job.JobId);

        retrieved.Should().NotBeNull();
        retrieved!.JobId.Should().Be(job.JobId);
        retrieved.SourceHostname.Should().Be("SOURCE-PC");
        retrieved.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task GetJobAsync_ReturnsNullForMissingJob()
    {
        var result = await _logger.GetJobAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_UpdatesPersistedJob()
    {
        var job = CreateTestJob();
        await _logger.CreateJobAsync(job);

        job.Status = JobStatus.InProgress;
        job.StartedUtc = DateTime.UtcNow;
        await _logger.UpdateJobAsync(job);

        var retrieved = await _logger.GetJobAsync(job.JobId);
        retrieved!.Status.Should().Be(JobStatus.InProgress);
        retrieved.StartedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_ThrowsForMissingJob()
    {
        var job = CreateTestJob();

        var act = async () => await _logger.UpdateJobAsync(job);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ListJobsAsync_ReturnsAllJobs()
    {
        await _logger.CreateJobAsync(CreateTestJob("job1"));
        await _logger.CreateJobAsync(CreateTestJob("job2"));
        await _logger.CreateJobAsync(CreateTestJob("job3"));

        var jobs = await _logger.ListJobsAsync();

        jobs.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListJobsAsync_FiltersbyStatus()
    {
        var pending = CreateTestJob("pending1");
        var completed = CreateTestJob("completed1");
        completed.Status = JobStatus.Completed;

        await _logger.CreateJobAsync(pending);
        await _logger.CreateJobAsync(completed);

        var result = await _logger.ListJobsAsync(JobStatus.Completed);

        result.Should().HaveCount(1);
        result[0].JobId.Should().Be("completed1");
    }

    [Fact]
    public async Task ListJobsAsync_ReturnsEmptyForNoJobs()
    {
        var jobs = await _logger.ListJobsAsync();

        jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateReportAsync_CreatesReportFromJob()
    {
        var job = CreateTestJob();
        job.Status = JobStatus.Completed;
        job.StartedUtc = DateTime.UtcNow.AddMinutes(-5);
        job.CompletedUtc = DateTime.UtcNow;
        job.Items.Add(new MigrationItem
        {
            DisplayName = "Chrome",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.Package,
            Status = MigrationItemStatus.Completed,
            EstimatedSizeBytes = 1000
        });
        job.Items.Add(new MigrationItem
        {
            DisplayName = "CustomApp",
            ItemType = MigrationItemType.Application,
            RecommendedTier = MigrationTier.RegistryFile,
            Status = MigrationItemStatus.Failed,
            StatusMessage = "Registry import failed"
        });
        await _logger.CreateJobAsync(job);

        var report = await _logger.GenerateReportAsync(job.JobId);

        report.JobId.Should().Be(job.JobId);
        report.FinalStatus.Should().Be(JobStatus.Completed);
        report.Summary.TotalItems.Should().Be(2);
        report.Summary.Completed.Should().Be(1);
        report.Summary.Failed.Should().Be(1);
        report.Errors.Should().ContainSingle(e => e.Contains("CustomApp"));
    }

    [Fact]
    public async Task GenerateReportAsync_ThrowsForMissingJob()
    {
        var act = async () => await _logger.GenerateReportAsync("nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportReportAsync_WritesReportToFile()
    {
        var report = new JobReport
        {
            JobId = "test123",
            FinalStatus = JobStatus.Completed
        };
        var outputPath = Path.Combine(_tempDir, "export", "report.json");

        await _logger.ExportReportAsync(report, outputPath);

        File.Exists(outputPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("test123");
    }

    [Fact]
    public async Task CreateJobAsync_AssignsIdWhenEmpty()
    {
        var job = new MigrationJob { JobId = "" };

        var created = await _logger.CreateJobAsync(job);

        created.JobId.Should().NotBeNullOrWhiteSpace();
    }

    private static MigrationJob CreateTestJob(string? id = null) => new()
    {
        JobId = id ?? Guid.NewGuid().ToString("N"),
        SourceHostname = "SOURCE-PC",
        DestinationHostname = "DEST-PC",
        TechnicianName = "TestTech"
    };
}
