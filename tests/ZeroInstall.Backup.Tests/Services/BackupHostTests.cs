using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.Backup.Infrastructure;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tests.Services;

public class BackupHostTests : IDisposable
{
    private readonly string _tempDir;

    public BackupHostTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-host-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void BuildHost_ResolvesAllServices()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "test-host",
            NasConnection = { Host = "nas.local", Username = "user", Password = "pass", RemoteBasePath = "/backups" }
        };

        using var host = BackupHost.BuildHost(config, Path.Combine(_tempDir, "config.json"), serviceMode: false);

        var executor = host.Services.GetService<IBackupExecutor>();
        executor.Should().NotBeNull();

        var scheduler = host.Services.GetService<IBackupScheduler>();
        scheduler.Should().NotBeNull();

        var configSync = host.Services.GetService<IConfigSyncService>();
        configSync.Should().NotBeNull();

        var statusReporter = host.Services.GetService<IStatusReporter>();
        statusReporter.Should().NotBeNull();

        var indexService = host.Services.GetService<IFileIndexService>();
        indexService.Should().NotBeNull();

        var retentionService = host.Services.GetService<IRetentionService>();
        retentionService.Should().NotBeNull();
    }

    [Fact]
    public void BuildHost_SchedulerIsSingleton()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "test-singleton",
            NasConnection = { Host = "nas.local", Username = "user", Password = "pass", RemoteBasePath = "/backups" }
        };

        using var host = BackupHost.BuildHost(config, Path.Combine(_tempDir, "config.json"), serviceMode: false);

        var scheduler1 = host.Services.GetService<IBackupScheduler>();
        var scheduler2 = host.Services.GetService<IBackupScheduler>();

        scheduler1.Should().BeSameAs(scheduler2);
    }
}
