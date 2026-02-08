using System.Text.Json;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class MigrationJobTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void NewJob_HasDefaultValues()
    {
        var job = new MigrationJob();

        job.JobId.Should().NotBeNullOrEmpty();
        job.Status.Should().Be(JobStatus.Pending);
        job.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        job.Items.Should().BeEmpty();
        job.UserMappings.Should().BeEmpty();
        job.Duration.Should().BeNull();
    }

    [Fact]
    public void Duration_WhenStartedAndCompleted_ReturnsCorrectValue()
    {
        var job = new MigrationJob
        {
            StartedUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc)
        };

        job.Duration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesAllProperties()
    {
        var job = new MigrationJob
        {
            SourceHostname = "OLD-PC",
            DestinationHostname = "NEW-PC",
            SourceOsVersion = "Windows 10 Pro",
            DestinationOsVersion = "Windows 11 Pro",
            TechnicianName = "TestTech",
            ProfileName = "Standard Office PC",
            TransportMethod = TransportMethod.NetworkShare,
            Status = JobStatus.Completed,
            StartedUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 1, 1, 10, 45, 0, DateTimeKind.Utc),
            Items =
            [
                new MigrationItem
                {
                    DisplayName = "Google Chrome",
                    ItemType = MigrationItemType.Application,
                    RecommendedTier = MigrationTier.Package,
                    IsSelected = true,
                    EstimatedSizeBytes = 500_000_000
                }
            ]
        };

        var json = JsonSerializer.Serialize(job, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MigrationJob>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be(job.JobId);
        deserialized.SourceHostname.Should().Be("OLD-PC");
        deserialized.DestinationHostname.Should().Be("NEW-PC");
        deserialized.TechnicianName.Should().Be("TestTech");
        deserialized.Status.Should().Be(JobStatus.Completed);
        deserialized.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        deserialized.Items.Should().HaveCount(1);
        deserialized.Items[0].DisplayName.Should().Be("Google Chrome");
    }
}
