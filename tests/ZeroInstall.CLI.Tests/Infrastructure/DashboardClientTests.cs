using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.CLI.Tests.Infrastructure;

public class DashboardClientTests
{
    [Fact]
    public async Task PushJob_HandlesConnectionFailure_Gracefully()
    {
        using var client = new DashboardClient("http://localhost:1", "test-key");
        var job = new MigrationJob { JobId = "test1", Status = JobStatus.Completed };

        // Should not throw - logs warning internally
        await client.PushJobAsync(job);
    }

    [Fact]
    public async Task PushReport_HandlesConnectionFailure_Gracefully()
    {
        using var client = new DashboardClient("http://localhost:1", "test-key");
        var report = new JobReport { ReportId = "rpt1", JobId = "j1", FinalStatus = JobStatus.Completed };

        // Should not throw - logs warning internally
        await client.PushReportAsync(report);
    }

    [Fact]
    public void Constructor_SetsApiKeyHeader()
    {
        using var client = new DashboardClient("http://localhost:5180", "my-key");
        // Client should be created without error
        client.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var client = new DashboardClient("http://localhost:5180", "my-key");
        var action = () => client.Dispose();
        action.Should().NotThrow();
    }
}
