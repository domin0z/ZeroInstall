using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tests.Services;

public class BackupSchedulerServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IBackupExecutor _mockExecutor;
    private readonly IConfigSyncService _mockConfigSync;
    private readonly IStatusReporter _mockStatusReporter;

    public BackupSchedulerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-sched-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockExecutor = Substitute.For<IBackupExecutor>();
        _mockConfigSync = Substitute.For<IConfigSyncService>();
        _mockStatusReporter = Substitute.For<IStatusReporter>();

        // Default: config sync returns same config
        _mockConfigSync.SyncConfigAsync(Arg.Any<BackupConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<BackupConfiguration>());
        _mockConfigSync.SaveLocalConfigAsync(Arg.Any<BackupConfiguration>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private BackupSchedulerService CreateService(BackupConfiguration? config = null)
    {
        config ??= new BackupConfiguration
        {
            CustomerId = "test-sched",
            FileBackupCron = "0 2 * * *",
            ConfigSyncIntervalMinutes = 1
        };

        return new BackupSchedulerService(
            _mockExecutor,
            _mockConfigSync,
            _mockStatusReporter,
            config,
            Path.Combine(_tempDir, "config.json"),
            NullLogger<BackupSchedulerService>.Instance);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        var service = CreateService();
        service.State.Should().Be(BackupSchedulerState.Idle);
    }

    [Fact]
    public async Task TriggerBackupNowAsync_RunsBackup()
    {
        _mockExecutor.RunFileBackupAsync(Arg.Any<BackupConfiguration>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BackupRunResult { ResultType = BackupRunResultType.Success, FilesUploaded = 5 });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        BackupRunResult? capturedResult = null;
        service.BackupCompleted += result => capturedResult = result;

        // Start the service
        var executeTask = Task.Run(async () =>
        {
            await ((Microsoft.Extensions.Hosting.BackgroundService)service).StartAsync(cts.Token);
            // Give it time to enter the wait loop
            await Task.Delay(500);
            // Trigger a manual backup
            await service.TriggerBackupNowAsync(cts.Token);
            // Wait for execution
            await Task.Delay(1000);
            await ((Microsoft.Extensions.Hosting.BackgroundService)service).StopAsync(CancellationToken.None);
        });

        await executeTask;

        await _mockExecutor.Received().RunFileBackupAsync(
            Arg.Any<BackupConfiguration>(),
            Arg.Any<IProgress<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StateChanged_FiresOnStateTransitions()
    {
        _mockExecutor.RunFileBackupAsync(Arg.Any<BackupConfiguration>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BackupRunResult { ResultType = BackupRunResultType.Success });

        var service = CreateService();
        var states = new List<BackupSchedulerState>();
        service.StateChanged += state => states.Add(state);

        var cts = new CancellationTokenSource();

        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StartAsync(cts.Token);
        await Task.Delay(300);
        await service.TriggerBackupNowAsync(cts.Token);
        await Task.Delay(1000);
        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StopAsync(CancellationToken.None);

        states.Should().Contain(BackupSchedulerState.Waiting);
        states.Should().Contain(BackupSchedulerState.Running);
    }

    [Fact]
    public async Task BackupCompleted_ReportsStatusToNas()
    {
        _mockExecutor.RunFileBackupAsync(Arg.Any<BackupConfiguration>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BackupRunResult { ResultType = BackupRunResultType.Success, FilesUploaded = 3 });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StartAsync(cts.Token);
        await Task.Delay(300);
        await service.TriggerBackupNowAsync(cts.Token);
        await Task.Delay(1000);
        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StopAsync(CancellationToken.None);

        await _mockStatusReporter.Received().ReportStatusAsync(
            Arg.Any<BackupConfiguration>(),
            Arg.Is<BackupStatus>(s => s.LastFilesUploaded == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfigSync_OccursDuringLoop()
    {
        _mockExecutor.RunFileBackupAsync(Arg.Any<BackupConfiguration>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BackupRunResult { ResultType = BackupRunResultType.Success });

        var config = new BackupConfiguration
        {
            CustomerId = "test-sync",
            FileBackupCron = "0 2 * * *",
            ConfigSyncIntervalMinutes = 0 // sync immediately
        };

        var service = CreateService(config);
        var cts = new CancellationTokenSource();

        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StartAsync(cts.Token);
        await Task.Delay(300);
        await service.TriggerBackupNowAsync(cts.Token);
        await Task.Delay(1000);
        await ((Microsoft.Extensions.Hosting.BackgroundService)service).StopAsync(CancellationToken.None);

        await _mockConfigSync.Received().SyncConfigAsync(
            Arg.Any<BackupConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NextScheduledUtc_IsNullInitially()
    {
        var service = CreateService();
        service.NextScheduledUtc.Should().BeNull();
    }
}
