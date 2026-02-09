using System.IO;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.App.Services;

/// <summary>
/// Orchestrates capture/restore by delegating to the appropriate Core services based on <see cref="ISessionState"/>.
/// </summary>
internal sealed class MigrationCoordinator : IMigrationCoordinator
{
    private readonly ISessionState _session;
    private readonly IPackageMigrator _packageMigrator;
    private readonly IRegistryMigrator _registryMigrator;
    private readonly IDiskCloner _diskCloner;
    private readonly ProfileSettingsMigratorService _profileSettings;
    private readonly IJobLogger _jobLogger;
    private readonly ILogger<MigrationCoordinator> _logger;

    public MigrationCoordinator(
        ISessionState session,
        IPackageMigrator packageMigrator,
        IRegistryMigrator registryMigrator,
        IDiskCloner diskCloner,
        ProfileSettingsMigratorService profileSettings,
        IJobLogger jobLogger,
        ILogger<MigrationCoordinator> logger)
    {
        _session = session;
        _packageMigrator = packageMigrator;
        _registryMigrator = registryMigrator;
        _diskCloner = diskCloner;
        _profileSettings = profileSettings;
        _jobLogger = jobLogger;
        _logger = logger;
    }

    public async Task CaptureAsync(
        IProgress<TransferProgress>? progress = null,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var job = new MigrationJob
        {
            SourceHostname = Environment.MachineName,
            SourceOsVersion = Environment.OSVersion.ToString(),
            TransportMethod = _session.TransportMethod,
            Status = JobStatus.InProgress,
            StartedUtc = DateTime.UtcNow,
            Items = _session.SelectedItems
        };
        await _jobLogger.CreateJobAsync(job, ct);
        _session.CurrentJob = job;

        var outputPath = _session.OutputPath;
        Directory.CreateDirectory(outputPath);

        try
        {
            var packageItems = _session.SelectedItems
                .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.Package)
                .ToList();
            var regFileItems = _session.SelectedItems
                .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.RegistryFile)
                .ToList();
            var profileItems = _session.SelectedItems
                .Where(i => i.IsSelected && (i.ItemType == MigrationItemType.UserProfile
                    || i.ItemType == MigrationItemType.SystemSetting
                    || i.ItemType == MigrationItemType.BrowserData))
                .ToList();

            if (packageItems.Count > 0)
            {
                statusProgress?.Report("Capturing package-based applications...");
                _logger.LogInformation("Capturing {Count} package items", packageItems.Count);
                await _packageMigrator.CaptureAsync(packageItems, outputPath, progress, ct);
            }

            if (regFileItems.Count > 0)
            {
                statusProgress?.Report("Capturing registry + file applications...");
                _logger.LogInformation("Capturing {Count} registry+file items", regFileItems.Count);
                await _registryMigrator.CaptureAsync(regFileItems, outputPath, progress, ct);
            }

            if (profileItems.Count > 0)
            {
                statusProgress?.Report("Capturing profiles and settings...");
                _logger.LogInformation("Capturing {Count} profile/settings items", profileItems.Count);
                var profileDir = Path.Combine(outputPath, "profile-settings");
                await _profileSettings.CaptureAsync(profileItems, profileDir, statusProgress, ct);
            }

            job.Status = JobStatus.Completed;
            job.CompletedUtc = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedUtc = DateTime.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture failed");
            job.Status = JobStatus.Failed;
            job.CompletedUtc = DateTime.UtcNow;
            throw;
        }
        finally
        {
            await _jobLogger.UpdateJobAsync(job, ct);
        }
    }

    public async Task RestoreAsync(
        IProgress<TransferProgress>? progress = null,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var job = new MigrationJob
        {
            DestinationHostname = Environment.MachineName,
            DestinationOsVersion = Environment.OSVersion.ToString(),
            TransportMethod = _session.TransportMethod,
            Status = JobStatus.InProgress,
            StartedUtc = DateTime.UtcNow,
            UserMappings = _session.UserMappings
        };
        await _jobLogger.CreateJobAsync(job, ct);
        _session.CurrentJob = job;

        var inputPath = _session.InputPath;

        try
        {
            statusProgress?.Report("Restoring package-based applications...");
            _logger.LogInformation("Restoring packages from {Path}", inputPath);
            await _packageMigrator.RestoreAsync(inputPath, _session.UserMappings, progress, ct);

            statusProgress?.Report("Restoring registry + file applications...");
            _logger.LogInformation("Restoring registry+files from {Path}", inputPath);
            await _registryMigrator.RestoreAsync(inputPath, _session.UserMappings, progress, ct);

            var profileDir = Path.Combine(inputPath, "profile-settings");
            if (Directory.Exists(profileDir))
            {
                statusProgress?.Report("Restoring profiles and settings...");
                _logger.LogInformation("Restoring profiles from {Path}", profileDir);
                await _profileSettings.RestoreAsync(profileDir, _session.UserMappings, statusProgress, ct);
            }

            job.Status = JobStatus.Completed;
            job.CompletedUtc = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedUtc = DateTime.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            job.Status = JobStatus.Failed;
            job.CompletedUtc = DateTime.UtcNow;
            throw;
        }
        finally
        {
            await _jobLogger.UpdateJobAsync(job, ct);
        }
    }
}
