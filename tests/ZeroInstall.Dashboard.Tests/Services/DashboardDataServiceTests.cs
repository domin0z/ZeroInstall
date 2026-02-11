using Microsoft.EntityFrameworkCore;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Data.Entities;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Services;

public class DashboardDataServiceTests
{
    private static (DashboardDbContext, DashboardDataService) CreateService()
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new DashboardDbContext(options);
        context.Database.EnsureCreated();
        return (context, new DashboardDataService(context));
    }

    [Fact]
    public async Task UpsertJob_CreatesNewRecord()
    {
        var (db, svc) = CreateService();
        var job = new MigrationJob { JobId = "new1", SourceHostname = "PC1", Status = JobStatus.InProgress };

        var record = await svc.UpsertJobAsync(job);

        record.JobId.Should().Be("new1");
        record.Status.Should().Be("InProgress");
        (await db.Jobs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertJob_UpdatesExistingRecord()
    {
        var (db, svc) = CreateService();
        var job = new MigrationJob { JobId = "upd1", Status = JobStatus.InProgress };
        await svc.UpsertJobAsync(job);

        job.Status = JobStatus.Completed;
        job.CompletedUtc = DateTime.UtcNow;
        await svc.UpsertJobAsync(job);

        (await db.Jobs.CountAsync()).Should().Be(1);
        var saved = await db.Jobs.FirstAsync();
        saved.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetJob_ReturnsNull_WhenNotFound()
    {
        var (_, svc) = CreateService();
        var result = await svc.GetJobAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJob_ReturnsRecord_WhenFound()
    {
        var (_, svc) = CreateService();
        await svc.UpsertJobAsync(new MigrationJob { JobId = "find1", SourceHostname = "SRC" });

        var result = await svc.GetJobAsync("find1");
        result.Should().NotBeNull();
        result!.SourceHostname.Should().Be("SRC");
    }

    [Fact]
    public async Task ListJobs_FiltersbyStatus()
    {
        var (_, svc) = CreateService();
        await svc.UpsertJobAsync(new MigrationJob { JobId = "a", Status = JobStatus.Completed });
        await svc.UpsertJobAsync(new MigrationJob { JobId = "b", Status = JobStatus.Failed });
        await svc.UpsertJobAsync(new MigrationJob { JobId = "c", Status = JobStatus.Completed });

        var completed = await svc.ListJobsAsync(statusFilter: "Completed");
        completed.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListJobs_Pagination()
    {
        var (_, svc) = CreateService();
        for (int i = 0; i < 10; i++)
            await svc.UpsertJobAsync(new MigrationJob { JobId = $"p{i}", Status = JobStatus.Completed });

        var page1 = await svc.ListJobsAsync(skip: 0, take: 3);
        page1.Should().HaveCount(3);

        var page2 = await svc.ListJobsAsync(skip: 3, take: 3);
        page2.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpsertReport_CreatesAndReturns()
    {
        var (_, svc) = CreateService();
        var report = new JobReport { ReportId = "rpt1", JobId = "job1", FinalStatus = JobStatus.Completed };

        var record = await svc.UpsertReportAsync(report);
        record.ReportId.Should().Be("rpt1");
        record.JobId.Should().Be("job1");
    }

    [Fact]
    public async Task GetReportByJobId_ReturnsReport()
    {
        var (_, svc) = CreateService();
        await svc.UpsertReportAsync(new JobReport { ReportId = "r1", JobId = "j1", FinalStatus = JobStatus.Completed });

        var result = await svc.GetReportByJobIdAsync("j1");
        result.Should().NotBeNull();
        result!.FinalStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task UpsertBackupStatus_CreatesNew()
    {
        var (_, svc) = CreateService();
        var json = "{\"lastRunResult\":\"Success\",\"nasUsageBytes\":5000,\"quotaBytes\":10000}";
        var record = await svc.UpsertBackupStatusAsync("cust1", "PC1", json);

        record.CustomerId.Should().Be("cust1");
        record.MachineName.Should().Be("PC1");
        record.NasUsageBytes.Should().Be(5000);
    }

    [Fact]
    public async Task ListBackupStatuses_ReturnsAll()
    {
        var (_, svc) = CreateService();
        await svc.UpsertBackupStatusAsync("c1", "M1", "{}");
        await svc.UpsertBackupStatusAsync("c2", "M2", "{}");

        var list = await svc.ListBackupStatusesAsync();
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAlert_AddsToDatabase()
    {
        var (db, svc) = CreateService();
        var alert = await svc.CreateAlertAsync("JobFailed", "j1", "Job failed");

        alert.AlertType.Should().Be("JobFailed");
        alert.IsActive.Should().BeTrue();
        (await db.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetActiveAlerts_ReturnsOnlyActive()
    {
        var (db, svc) = CreateService();
        await svc.CreateAlertAsync("JobFailed", "j1", "Active");
        var dismissed = await svc.CreateAlertAsync("BackupOverdue", "c1", "Dismissed");
        await svc.DismissAlertAsync(dismissed.Id);

        var alerts = await svc.GetActiveAlertsAsync();
        alerts.Should().HaveCount(1);
        alerts[0].Message.Should().Be("Active");
    }

    [Fact]
    public async Task DismissAlert_SetsInactiveAndTimestamp()
    {
        var (db, svc) = CreateService();
        var alert = await svc.CreateAlertAsync("JobFailed", "j1", "Test");
        await svc.DismissAlertAsync(alert.Id);

        var saved = await db.Alerts.FindAsync(alert.Id);
        saved!.IsActive.Should().BeFalse();
        saved.DismissedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStats_ReturnsCorrectCounts()
    {
        var (_, svc) = CreateService();
        await svc.UpsertJobAsync(new MigrationJob { JobId = "s1", Status = JobStatus.Completed });
        await svc.UpsertJobAsync(new MigrationJob { JobId = "s2", Status = JobStatus.InProgress });
        await svc.UpsertJobAsync(new MigrationJob { JobId = "s3", Status = JobStatus.Failed });
        await svc.UpsertBackupStatusAsync("c1", "M1", "{}");
        await svc.CreateAlertAsync("JobFailed", "s3", "Failed");

        var stats = await svc.GetStatsAsync();
        stats.TotalJobs.Should().Be(3);
        stats.ActiveJobs.Should().Be(1);
        stats.CompletedJobs.Should().Be(1);
        stats.FailedJobs.Should().Be(1);
        stats.TotalCustomers.Should().Be(1);
        stats.ActiveAlerts.Should().Be(1);
    }
}
