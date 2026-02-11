using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Data.Entities;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Services;

public class AlertServiceTests
{
    private static (DashboardDbContext, AlertService) CreateService()
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new DashboardDbContext(options);
        context.Database.EnsureCreated();
        return (context, new AlertService(context, NullLogger<AlertService>.Instance));
    }

    [Fact]
    public async Task EvaluateJob_CreatesAlert_WhenFailed()
    {
        var (db, svc) = CreateService();
        var job = new JobRecord { JobId = "f1", Status = "Failed", SourceHostname = "A", DestinationHostname = "B" };

        await svc.EvaluateJobAsync(job);

        var alerts = await db.Alerts.Where(a => a.IsActive).ToListAsync();
        alerts.Should().HaveCount(1);
        alerts[0].AlertType.Should().Be("JobFailed");
        alerts[0].RelatedId.Should().Be("f1");
    }

    [Fact]
    public async Task EvaluateJob_DoesNotCreateAlert_WhenCompleted()
    {
        var (db, svc) = CreateService();
        var job = new JobRecord { JobId = "c1", Status = "Completed" };

        await svc.EvaluateJobAsync(job);

        (await db.Alerts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EvaluateJob_AutoDismisses_WhenCompletedAfterFailure()
    {
        var (db, svc) = CreateService();
        var failed = new JobRecord { JobId = "ad1", Status = "Failed", SourceHostname = "A", DestinationHostname = "B" };
        await svc.EvaluateJobAsync(failed);

        var completed = new JobRecord { JobId = "ad1", Status = "Completed" };
        await svc.EvaluateJobAsync(completed);

        var active = await db.Alerts.Where(a => a.IsActive).ToListAsync();
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateJob_PreventsDuplicate_WhenAlreadyExists()
    {
        var (db, svc) = CreateService();
        var job = new JobRecord { JobId = "dup1", Status = "Failed", SourceHostname = "A", DestinationHostname = "B" };

        await svc.EvaluateJobAsync(job);
        await svc.EvaluateJobAsync(job);

        (await db.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EvaluateBackupStatus_CreatesOverdueAlert_WhenOld()
    {
        var (db, svc) = CreateService();
        var status = new BackupStatusRecord
        {
            CustomerId = "old1",
            MachineName = "PC1",
            LastBackupUtc = DateTime.UtcNow.AddHours(-72)
        };

        await svc.EvaluateBackupStatusAsync(status);

        var alerts = await db.Alerts.Where(a => a.AlertType == "BackupOverdue").ToListAsync();
        alerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateBackupStatus_NoAlert_WhenRecent()
    {
        var (db, svc) = CreateService();
        var status = new BackupStatusRecord
        {
            CustomerId = "recent1",
            MachineName = "PC1",
            LastBackupUtc = DateTime.UtcNow.AddHours(-1)
        };

        await svc.EvaluateBackupStatusAsync(status);

        (await db.Alerts.Where(a => a.AlertType == "BackupOverdue").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EvaluateBackupStatus_CreatesQuotaWarning_Over90Percent()
    {
        var (db, svc) = CreateService();
        var status = new BackupStatusRecord
        {
            CustomerId = "quota1",
            MachineName = "PC1",
            LastBackupUtc = DateTime.UtcNow,
            NasUsageBytes = 950,
            QuotaBytes = 1000
        };

        await svc.EvaluateBackupStatusAsync(status);

        var alerts = await db.Alerts.Where(a => a.AlertType == "QuotaWarning").ToListAsync();
        alerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateBackupStatus_AutoDismissesOverdue_WhenBackupResumes()
    {
        var (db, svc) = CreateService();
        var overdue = new BackupStatusRecord
        {
            CustomerId = "resume1",
            MachineName = "PC1",
            LastBackupUtc = DateTime.UtcNow.AddHours(-72)
        };
        await svc.EvaluateBackupStatusAsync(overdue);

        var resumed = new BackupStatusRecord
        {
            CustomerId = "resume1",
            MachineName = "PC1",
            LastBackupUtc = DateTime.UtcNow
        };
        await svc.EvaluateBackupStatusAsync(resumed);

        var active = await db.Alerts.Where(a => a.IsActive && a.AlertType == "BackupOverdue").ToListAsync();
        active.Should().BeEmpty();
    }
}
