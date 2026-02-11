using Microsoft.EntityFrameworkCore;
using ZeroInstall.Dashboard.Data;
using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Tests.Data;

public class DashboardDbContextTests
{
    private static DashboardDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new DashboardDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void CreatesTables_WhenDatabaseInitialized()
    {
        using var context = CreateInMemoryContext();

        context.Jobs.Should().NotBeNull();
        context.Reports.Should().NotBeNull();
        context.BackupStatuses.Should().NotBeNull();
        context.Alerts.Should().NotBeNull();
    }

    [Fact]
    public async Task CanInsert_JobRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new JobRecord
        {
            JobId = "abc123",
            RawJson = "{}",
            Status = "Completed",
            SourceHostname = "PC1",
            DestinationHostname = "PC2",
            TechnicianName = "Tech1",
            TotalItems = 10,
            CompletedItems = 8,
            FailedItems = 2
        };

        context.Jobs.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == "abc123");
        saved.Should().NotBeNull();
        saved!.Status.Should().Be("Completed");
        saved.TotalItems.Should().Be(10);
    }

    [Fact]
    public async Task CanUpdate_JobRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new JobRecord { JobId = "job1", RawJson = "{}", Status = "InProgress" };
        context.Jobs.Add(record);
        await context.SaveChangesAsync();

        record.Status = "Completed";
        record.CompletedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var saved = await context.Jobs.FirstAsync(j => j.JobId == "job1");
        saved.Status.Should().Be("Completed");
        saved.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CanDelete_JobRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new JobRecord { JobId = "del1", RawJson = "{}", Status = "Failed" };
        context.Jobs.Add(record);
        await context.SaveChangesAsync();

        context.Jobs.Remove(record);
        await context.SaveChangesAsync();

        var found = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == "del1");
        found.Should().BeNull();
    }

    [Fact]
    public async Task CanQuery_JobsByStatus()
    {
        using var context = CreateInMemoryContext();

        context.Jobs.AddRange(
            new JobRecord { JobId = "j1", RawJson = "{}", Status = "Completed" },
            new JobRecord { JobId = "j2", RawJson = "{}", Status = "Failed" },
            new JobRecord { JobId = "j3", RawJson = "{}", Status = "Completed" }
        );
        await context.SaveChangesAsync();

        var completed = await context.Jobs.Where(j => j.Status == "Completed").ToListAsync();
        completed.Should().HaveCount(2);
    }

    [Fact]
    public async Task CanInsert_BackupStatusRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new BackupStatusRecord
        {
            CustomerId = "cust1",
            MachineName = "DESKTOP-1",
            RawJson = "{}",
            LastRunResult = "Success",
            NasUsageBytes = 1024 * 1024 * 500,
            QuotaBytes = 1024L * 1024 * 1024 * 10
        };

        context.BackupStatuses.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.BackupStatuses.FirstAsync(b => b.CustomerId == "cust1");
        saved.MachineName.Should().Be("DESKTOP-1");
        saved.NasUsageBytes.Should().Be(1024 * 1024 * 500);
    }

    [Fact]
    public async Task CanUpdate_BackupStatusRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new BackupStatusRecord { CustomerId = "cust2", RawJson = "{}", LastRunResult = "Success" };
        context.BackupStatuses.Add(record);
        await context.SaveChangesAsync();

        record.LastRunResult = "Failed";
        record.LastBackupUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var saved = await context.BackupStatuses.FirstAsync(b => b.CustomerId == "cust2");
        saved.LastRunResult.Should().Be("Failed");
    }

    [Fact]
    public async Task CanDelete_BackupStatusRecord()
    {
        using var context = CreateInMemoryContext();

        var record = new BackupStatusRecord { CustomerId = "del-cust", RawJson = "{}" };
        context.BackupStatuses.Add(record);
        await context.SaveChangesAsync();

        context.BackupStatuses.Remove(record);
        await context.SaveChangesAsync();

        var found = await context.BackupStatuses.FirstOrDefaultAsync(b => b.CustomerId == "del-cust");
        found.Should().BeNull();
    }

    [Fact]
    public async Task CanQuery_BackupStatusesByCustomer()
    {
        using var context = CreateInMemoryContext();

        context.BackupStatuses.AddRange(
            new BackupStatusRecord { CustomerId = "c1", RawJson = "{}", LastRunResult = "Success" },
            new BackupStatusRecord { CustomerId = "c2", RawJson = "{}", LastRunResult = "Failed" }
        );
        await context.SaveChangesAsync();

        var found = await context.BackupStatuses.FirstOrDefaultAsync(b => b.CustomerId == "c2");
        found.Should().NotBeNull();
        found!.LastRunResult.Should().Be("Failed");
    }

    [Fact]
    public async Task JobRecord_RoundTrip_PreservesAllFields()
    {
        using var context = CreateInMemoryContext();
        var now = DateTime.UtcNow;

        var record = new JobRecord
        {
            JobId = "rt1",
            RawJson = "{\"key\":\"value\"}",
            Status = "InProgress",
            SourceHostname = "SRC",
            DestinationHostname = "DST",
            TechnicianName = "John",
            StartedUtc = now,
            CompletedUtc = now.AddHours(1),
            CreatedUtc = now.AddHours(-1),
            ImportedUtc = now,
            TotalItems = 5,
            CompletedItems = 3,
            FailedItems = 1,
            TotalBytesTransferred = 1024 * 1024 * 100
        };

        context.Jobs.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.Jobs.FirstAsync(j => j.JobId == "rt1");
        saved.RawJson.Should().Be("{\"key\":\"value\"}");
        saved.SourceHostname.Should().Be("SRC");
        saved.DestinationHostname.Should().Be("DST");
        saved.TechnicianName.Should().Be("John");
        saved.TotalBytesTransferred.Should().Be(1024 * 1024 * 100);
    }

    [Fact]
    public async Task BackupStatusRecord_RoundTrip_PreservesAllFields()
    {
        using var context = CreateInMemoryContext();
        var now = DateTime.UtcNow;

        var record = new BackupStatusRecord
        {
            CustomerId = "rt-cust",
            MachineName = "MACHINE",
            RawJson = "{\"backup\":true}",
            LastRunResult = "Success",
            LastBackupUtc = now,
            NextScheduledUtc = now.AddDays(1),
            NasUsageBytes = 5000,
            QuotaBytes = 10000,
            UpdatedUtc = now
        };

        context.BackupStatuses.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.BackupStatuses.FirstAsync(b => b.CustomerId == "rt-cust");
        saved.MachineName.Should().Be("MACHINE");
        saved.RawJson.Should().Contain("backup");
        saved.NasUsageBytes.Should().Be(5000);
        saved.QuotaBytes.Should().Be(10000);
    }

    [Fact]
    public async Task AlertRecord_RoundTrip_PreservesAllFields()
    {
        using var context = CreateInMemoryContext();
        var now = DateTime.UtcNow;

        var record = new AlertRecord
        {
            AlertType = "JobFailed",
            RelatedId = "job123",
            Message = "Migration job failed",
            CreatedUtc = now,
            IsActive = true
        };

        context.Alerts.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.Alerts.FirstAsync(a => a.RelatedId == "job123");
        saved.AlertType.Should().Be("JobFailed");
        saved.Message.Should().Be("Migration job failed");
        saved.IsActive.Should().BeTrue();
        saved.DismissedUtc.Should().BeNull();
    }

    [Fact]
    public async Task AlertRecord_CanDismiss()
    {
        using var context = CreateInMemoryContext();

        var record = new AlertRecord
        {
            AlertType = "BackupOverdue",
            RelatedId = "cust1",
            Message = "Backup overdue",
            IsActive = true
        };

        context.Alerts.Add(record);
        await context.SaveChangesAsync();

        record.IsActive = false;
        record.DismissedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var saved = await context.Alerts.FirstAsync(a => a.RelatedId == "cust1");
        saved.IsActive.Should().BeFalse();
        saved.DismissedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task JobReportRecord_RoundTrip_PreservesAllFields()
    {
        using var context = CreateInMemoryContext();

        var record = new JobReportRecord
        {
            ReportId = "rpt1",
            JobId = "job1",
            RawJson = "{\"report\":true}",
            FinalStatus = "Completed",
            GeneratedUtc = DateTime.UtcNow
        };

        context.Reports.Add(record);
        await context.SaveChangesAsync();

        var saved = await context.Reports.FirstAsync(r => r.ReportId == "rpt1");
        saved.JobId.Should().Be("job1");
        saved.FinalStatus.Should().Be("Completed");
        saved.RawJson.Should().Contain("report");
    }

    [Fact]
    public void DashboardConfiguration_HasSensibleDefaults()
    {
        var config = new ZeroInstall.Dashboard.Services.DashboardConfiguration();

        config.DatabasePath.Should().Be("dashboard.db");
        config.NasSftpHost.Should().BeNull();
        config.NasSftpPort.Should().Be(22);
        config.NasSftpBasePath.Should().Be("/backups/zim");
        config.NasScanIntervalMinutes.Should().Be(5);
        config.ApiKey.Should().NotBeNullOrEmpty();
        config.ListenPort.Should().Be(5180);
    }

    [Fact]
    public void DashboardConfiguration_ApiKey_UniquePerInstance()
    {
        var config1 = new ZeroInstall.Dashboard.Services.DashboardConfiguration();
        var config2 = new ZeroInstall.Dashboard.Services.DashboardConfiguration();

        config1.ApiKey.Should().NotBe(config2.ApiKey);
    }

    [Fact]
    public void DashboardConfiguration_CanSetAllProperties()
    {
        var config = new ZeroInstall.Dashboard.Services.DashboardConfiguration
        {
            DatabasePath = "custom.db",
            NasSftpHost = "nas.local",
            NasSftpPort = 2222,
            NasSftpUser = "admin",
            NasSftpPassword = "secret",
            NasSftpKeyPath = "/path/to/key",
            NasSftpBasePath = "/custom/path",
            NasScanIntervalMinutes = 10,
            ApiKey = "custom-key",
            ListenPort = 8080
        };

        config.DatabasePath.Should().Be("custom.db");
        config.NasSftpHost.Should().Be("nas.local");
        config.NasSftpPort.Should().Be(2222);
        config.NasSftpUser.Should().Be("admin");
        config.NasSftpPassword.Should().Be("secret");
        config.NasSftpKeyPath.Should().Be("/path/to/key");
        config.NasSftpBasePath.Should().Be("/custom/path");
        config.NasScanIntervalMinutes.Should().Be(10);
        config.ApiKey.Should().Be("custom-key");
        config.ListenPort.Should().Be(8080);
    }

    [Fact]
    public async Task CanQueryActiveAlerts()
    {
        using var context = CreateInMemoryContext();

        context.Alerts.AddRange(
            new AlertRecord { AlertType = "JobFailed", Message = "Active", IsActive = true },
            new AlertRecord { AlertType = "BackupOverdue", Message = "Dismissed", IsActive = false }
        );
        await context.SaveChangesAsync();

        var active = await context.Alerts.Where(a => a.IsActive).ToListAsync();
        active.Should().HaveCount(1);
        active[0].Message.Should().Be("Active");
    }
}
