using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Tests.Services;

public class RetentionServiceTests
{
    private readonly RetentionService _service;

    public RetentionServiceTests()
    {
        _service = new RetentionService(NullLogger<RetentionService>.Instance);
    }

    [Fact]
    public async Task CalculateNasUsageAsync_ReturnsZero_WhenPathDoesNotExist()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/backups/customers/cust1").Returns(false);

        var usage = await _service.CalculateNasUsageAsync(client, "/backups/customers/cust1");

        usage.Should().Be(0);
    }

    [Fact]
    public async Task CalculateNasUsageAsync_SumsFileSizes()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/data").Returns(true);
        client.ListDirectory("/data").Returns(new List<SftpFileInfo>
        {
            new("file1.dat", "/data/file1.dat", false, 1000, DateTime.UtcNow),
            new("file2.dat", "/data/file2.dat", false, 2000, DateTime.UtcNow),
            new(".", "/data/.", true, 0, DateTime.UtcNow),
            new("..", "/data/..", true, 0, DateTime.UtcNow),
        });

        var usage = await _service.CalculateNasUsageAsync(client, "/data");

        usage.Should().Be(3000);
    }

    [Fact]
    public async Task CalculateNasUsageAsync_RecursesIntoSubdirectories()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/data").Returns(true);
        client.ListDirectory("/data").Returns(new List<SftpFileInfo>
        {
            new("sub", "/data/sub", true, 0, DateTime.UtcNow),
            new(".", "/data/.", true, 0, DateTime.UtcNow),
            new("..", "/data/..", true, 0, DateTime.UtcNow),
        });
        client.ListDirectory("/data/sub").Returns(new List<SftpFileInfo>
        {
            new("nested.txt", "/data/sub/nested.txt", false, 500, DateTime.UtcNow),
            new(".", "/data/sub/.", true, 0, DateTime.UtcNow),
            new("..", "/data/sub/..", true, 0, DateTime.UtcNow),
        });

        var usage = await _service.CalculateNasUsageAsync(client, "/data");

        usage.Should().Be(500);
    }

    [Fact]
    public async Task ListBackupRunsAsync_ReturnsEmptyForMissingPath()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/backups").Returns(false);

        var runs = await _service.ListBackupRunsAsync(client, "/backups");

        runs.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackupRunsAsync_ReturnsDirectoriesSorted()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/backups").Returns(true);
        client.ListDirectory("/backups").Returns(new List<SftpFileInfo>
        {
            new("2026-02-10T020000Z", "/backups/2026-02-10T020000Z", true, 0, DateTime.UtcNow),
            new("2026-02-08T020000Z", "/backups/2026-02-08T020000Z", true, 0, DateTime.UtcNow),
            new("2026-02-09T020000Z", "/backups/2026-02-09T020000Z", true, 0, DateTime.UtcNow),
            new(".", "/backups/.", true, 0, DateTime.UtcNow),
            new("..", "/backups/..", true, 0, DateTime.UtcNow),
        });

        var runs = await _service.ListBackupRunsAsync(client, "/backups");

        runs.Should().HaveCount(3);
        runs[0].Should().Contain("2026-02-08");
        runs[1].Should().Contain("2026-02-09");
        runs[2].Should().Contain("2026-02-10");
    }

    [Fact]
    public async Task EnforceRetentionAsync_DeletesOldestBeyondLimit()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/backups").Returns(true);
        client.ListDirectory("/backups").Returns(new List<SftpFileInfo>
        {
            new("run1", "/backups/run1", true, 0, DateTime.UtcNow),
            new("run2", "/backups/run2", true, 0, DateTime.UtcNow),
            new("run3", "/backups/run3", true, 0, DateTime.UtcNow),
            new(".", "/backups/.", true, 0, DateTime.UtcNow),
            new("..", "/backups/..", true, 0, DateTime.UtcNow),
        });

        // Each run directory has one file
        foreach (var name in new[] { "run1", "run2", "run3" })
        {
            client.ListDirectory($"/backups/{name}").Returns(new List<SftpFileInfo>
            {
                new("data.zip", $"/backups/{name}/data.zip", false, 100, DateTime.UtcNow),
                new(".", $"/backups/{name}/.", true, 0, DateTime.UtcNow),
                new("..", $"/backups/{name}/..", true, 0, DateTime.UtcNow),
            });
        }

        var deleted = await _service.EnforceRetentionAsync(client, "/backups", keepLast: 2);

        deleted.Should().Be(1);
        client.Received(1).DeleteFile("/backups/run1/data.zip");
    }

    [Fact]
    public async Task EnforceRetentionAsync_DoesNothing_WhenWithinLimit()
    {
        var client = Substitute.For<ISftpClientWrapper>();
        client.Exists("/backups").Returns(true);
        client.ListDirectory("/backups").Returns(new List<SftpFileInfo>
        {
            new("run1", "/backups/run1", true, 0, DateTime.UtcNow),
            new("run2", "/backups/run2", true, 0, DateTime.UtcNow),
            new(".", "/backups/.", true, 0, DateTime.UtcNow),
            new("..", "/backups/..", true, 0, DateTime.UtcNow),
        });

        var deleted = await _service.EnforceRetentionAsync(client, "/backups", keepLast: 5);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task EnforceRetentionAsync_ReturnsZero_WhenKeepLastIsZero()
    {
        var client = Substitute.For<ISftpClientWrapper>();

        var deleted = await _service.EnforceRetentionAsync(client, "/backups", keepLast: 0);

        deleted.Should().Be(0);
    }
}
