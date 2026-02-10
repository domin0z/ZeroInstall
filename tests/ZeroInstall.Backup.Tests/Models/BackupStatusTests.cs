using System.Text.Json;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests.Models;

public class BackupStatusTests
{
    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var status = new BackupStatus
        {
            CustomerId = "cust-001",
            MachineName = "DESKTOP-ABC",
            AgentVersion = "0.1.0",
            LastRunId = "run-123",
            LastRunResult = BackupRunResultType.Success,
            LastBackupUtc = new DateTime(2026, 2, 10, 2, 15, 0, DateTimeKind.Utc),
            LastFilesUploaded = 42,
            LastBytesTransferred = 1_000_000,
            NextScheduledUtc = new DateTime(2026, 2, 11, 2, 0, 0, DateTimeKind.Utc),
            NasUsageBytes = 5_000_000_000L,
            QuotaBytes = 10_000_000_000L
        };

        var json = JsonSerializer.Serialize(status);
        var deserialized = JsonSerializer.Deserialize<BackupStatus>(json)!;

        deserialized.CustomerId.Should().Be("cust-001");
        deserialized.MachineName.Should().Be("DESKTOP-ABC");
        deserialized.LastRunResult.Should().Be(BackupRunResultType.Success);
        deserialized.LastFilesUploaded.Should().Be(42);
        deserialized.NasUsageBytes.Should().Be(5_000_000_000L);
        deserialized.QuotaBytes.Should().Be(10_000_000_000L);
    }
}
