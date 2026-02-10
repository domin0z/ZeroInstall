using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Background service that runs scheduled backups based on cron expressions.
/// Also periodically syncs config from NAS.
/// </summary>
internal class BackupSchedulerService : BackgroundService, IBackupScheduler
{
    private readonly IBackupExecutor _executor;
    private readonly IConfigSyncService _configSync;
    private readonly IStatusReporter _statusReporter;
    private readonly ILogger<BackupSchedulerService> _logger;

    private BackupConfiguration _config;
    private readonly string _configPath;
    private BackupSchedulerState _state = BackupSchedulerState.Idle;
    private DateTime? _nextScheduledUtc;
    private readonly SemaphoreSlim _triggerSemaphore = new(0, 1);

    public BackupSchedulerState State => _state;
    public DateTime? NextScheduledUtc => _nextScheduledUtc;
    public event Action<BackupSchedulerState>? StateChanged;
    public event Action<BackupRunResult>? BackupCompleted;

    public BackupSchedulerService(
        IBackupExecutor executor,
        IConfigSyncService configSync,
        IStatusReporter statusReporter,
        BackupConfiguration config,
        string configPath,
        ILogger<BackupSchedulerService> logger)
    {
        _executor = executor;
        _configSync = configSync;
        _statusReporter = statusReporter;
        _config = config;
        _configPath = configPath;
        _logger = logger;
    }

    public Task TriggerBackupNowAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Manual backup triggered");
        try
        {
            _triggerSemaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already triggered, ignore
        }
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup scheduler started for customer {CustomerId}", _config.CustomerId);

        DateTime lastConfigSync = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Config sync
                if ((DateTime.UtcNow - lastConfigSync).TotalMinutes >= _config.ConfigSyncIntervalMinutes)
                {
                    _config = await _configSync.SyncConfigAsync(_config, stoppingToken);
                    await _configSync.SaveLocalConfigAsync(_config, _configPath, stoppingToken);
                    lastConfigSync = DateTime.UtcNow;
                }

                // Calculate next run time
                var nextFileBackup = GetNextOccurrence(_config.FileBackupCron);
                var nextFullImage = _config.EnableFullImageBackup
                    ? GetNextOccurrence(_config.FullImageCron)
                    : (DateTime?)null;

                _nextScheduledUtc = nextFullImage.HasValue && nextFullImage < nextFileBackup
                    ? nextFullImage
                    : nextFileBackup;

                SetState(BackupSchedulerState.Waiting);

                if (_nextScheduledUtc == null)
                {
                    _logger.LogWarning("No valid schedule, waiting 1 hour before retry");
                    await WaitOrTrigger(TimeSpan.FromHours(1), stoppingToken);
                    continue;
                }

                var delay = _nextScheduledUtc.Value - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next backup at {NextRun} (in {Delay})",
                        _nextScheduledUtc.Value, delay);

                    var triggered = await WaitOrTrigger(delay, stoppingToken);
                    if (!triggered && stoppingToken.IsCancellationRequested)
                        break;
                }

                // Run the appropriate backup
                SetState(BackupSchedulerState.Running);

                bool isFullImage = nextFullImage.HasValue &&
                    (!nextFileBackup.HasValue || nextFullImage <= nextFileBackup);

                BackupRunResult result;

                if (isFullImage && _config.EnableFullImageBackup)
                {
                    _logger.LogInformation("Starting scheduled full image backup");
                    result = await _executor.RunFullImageBackupAsync(_config, ct: stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Starting scheduled file backup");
                    result = await _executor.RunFileBackupAsync(_config, ct: stoppingToken);
                }

                _logger.LogInformation("Backup completed: {Result} ({Uploaded} files, {Bytes} bytes)",
                    result.ResultType, result.FilesUploaded, result.BytesTransferred);

                BackupCompleted?.Invoke(result);

                // Report status to NAS
                await ReportStatus(result, stoppingToken);

                SetState(BackupSchedulerState.Idle);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler loop error, will retry in 5 minutes");
                SetState(BackupSchedulerState.Idle);

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Backup scheduler stopped");
    }

    private async Task<bool> WaitOrTrigger(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            return await _triggerSemaphore.WaitAsync(delay, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private DateTime? GetNextOccurrence(string cronExpression)
    {
        try
        {
            var schedule = CrontabSchedule.Parse(cronExpression);
            return schedule.GetNextOccurrence(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid cron expression: {Cron}", cronExpression);
            return null;
        }
    }

    private void SetState(BackupSchedulerState newState)
    {
        _state = newState;
        StateChanged?.Invoke(newState);
    }

    private async Task ReportStatus(BackupRunResult result, CancellationToken ct)
    {
        try
        {
            var status = new BackupStatus
            {
                CustomerId = _config.CustomerId,
                MachineName = Environment.MachineName,
                AgentVersion = typeof(BackupSchedulerService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                LastRunId = result.RunId,
                LastRunResult = result.ResultType,
                LastBackupUtc = result.CompletedUtc,
                LastFilesUploaded = result.FilesUploaded,
                LastBytesTransferred = result.BytesTransferred,
                NextScheduledUtc = _nextScheduledUtc,
                QuotaBytes = _config.QuotaBytes,
                UpdatedUtc = DateTime.UtcNow
            };

            await _statusReporter.ReportStatusAsync(_config, status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report status after backup");
        }
    }
}
