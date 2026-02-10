using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Tests.Services;

public class ConfigSyncServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ISftpClientWrapper _mockClient;
    private readonly ISftpClientFactory _mockFactory;
    private readonly ConfigSyncService _service;

    public ConfigSyncServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockClient = Substitute.For<ISftpClientWrapper>();
        _mockFactory = Substitute.For<ISftpClientFactory>();
        _mockFactory.Create(Arg.Any<SftpTransportConfiguration>()).Returns(_mockClient);

        _service = new ConfigSyncService(_mockFactory, NullLogger<ConfigSyncService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SyncConfigAsync_ReturnsLocal_WhenNoRemote()
    {
        var local = new BackupConfiguration { CustomerId = "local", NasConnection = { RemoteBasePath = "/backups" } };
        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        var result = await _service.SyncConfigAsync(local);

        result.CustomerId.Should().Be("local");
    }

    [Fact]
    public async Task SyncConfigAsync_UsesRemote_WhenNewer()
    {
        var local = new BackupConfiguration
        {
            CustomerId = "local",
            LastModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NasConnection = { RemoteBasePath = "/backups", Host = "nas" }
        };

        var remote = new BackupConfiguration
        {
            CustomerId = "remote",
            FileBackupCron = "0 4 * * *",
            LastModifiedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        var remoteJson = JsonSerializer.Serialize(remote);
        _mockClient.OpenRead(Arg.Any<string>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(remoteJson)));

        var result = await _service.SyncConfigAsync(local);

        result.CustomerId.Should().Be("remote");
        result.FileBackupCron.Should().Be("0 4 * * *");
        // NAS connection should be preserved from local
        result.NasConnection.Host.Should().Be("nas");
    }

    [Fact]
    public async Task SyncConfigAsync_KeepsLocal_WhenLocalIsNewer()
    {
        var local = new BackupConfiguration
        {
            CustomerId = "local",
            LastModifiedUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            NasConnection = { RemoteBasePath = "/backups" }
        };

        var remote = new BackupConfiguration
        {
            CustomerId = "remote",
            LastModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        var remoteJson = JsonSerializer.Serialize(remote);
        _mockClient.OpenRead(Arg.Any<string>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(remoteJson)));

        var result = await _service.SyncConfigAsync(local);

        result.CustomerId.Should().Be("local");
    }

    [Fact]
    public async Task SaveAndLoadLocalConfig_RoundTrips()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            DisplayName = "Test PC",
            BackupPaths = { @"C:\Users\Test\Documents" },
            FileBackupCron = "0 3 * * *"
        };

        await _service.SaveLocalConfigAsync(config, configPath);
        var loaded = await _service.LoadLocalConfigAsync(configPath);

        loaded.Should().NotBeNull();
        loaded!.CustomerId.Should().Be("cust-001");
        loaded.DisplayName.Should().Be("Test PC");
        loaded.BackupPaths.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadLocalConfigAsync_ReturnsNull_WhenMissing()
    {
        var result = await _service.LoadLocalConfigAsync(Path.Combine(_tempDir, "nope.json"));
        result.Should().BeNull();
    }
}
