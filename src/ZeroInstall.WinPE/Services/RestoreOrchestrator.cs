using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.WinPE.Services;

/// <summary>
/// Options controlling the restore workflow.
/// </summary>
public class RestoreOptions
{
    public bool SkipVerify { get; set; }
    public string? DriverPath { get; set; }
    public bool Recurse { get; set; } = true;
}

/// <summary>
/// Result of a restore operation.
/// </summary>
public class RestoreResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public DriverInjectionResult? DriverResult { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Orchestrates the full restore workflow: verify → restore → driver injection.
/// </summary>
public class RestoreOrchestrator
{
    private readonly IDiskCloner _diskCloner;
    private readonly DriverInjectionService _driverInjection;
    private readonly ILogger<RestoreOrchestrator> _logger;

    public RestoreOrchestrator(
        IDiskCloner diskCloner,
        DriverInjectionService driverInjection,
        ILogger<RestoreOrchestrator> logger)
    {
        _diskCloner = diskCloner;
        _driverInjection = driverInjection;
        _logger = logger;
    }

    /// <summary>
    /// Runs the complete restore workflow.
    /// </summary>
    public async Task<RestoreResult> RunRestoreAsync(
        string imagePath,
        string targetVolumePath,
        RestoreOptions options,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Verify image integrity (optional)
            if (!options.SkipVerify)
            {
                _logger.LogInformation("Verifying image integrity: {Path}", imagePath);
                var isValid = await _diskCloner.VerifyImageAsync(imagePath, ct);
                if (!isValid)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Duration = sw.Elapsed,
                        Error = "Image integrity verification failed. The image may be corrupted."
                    };
                }
                _logger.LogInformation("Image verification passed");
            }

            // Step 2: Restore the image
            _logger.LogInformation("Restoring image {Image} to {Target}", imagePath, targetVolumePath);
            await _diskCloner.RestoreImageAsync(imagePath, targetVolumePath, progress, ct);
            _logger.LogInformation("Image restore complete");

            // Step 3: Driver injection (optional)
            DriverInjectionResult? driverResult = null;
            if (!string.IsNullOrEmpty(options.DriverPath))
            {
                _logger.LogInformation("Injecting drivers from {Path}", options.DriverPath);
                driverResult = await _driverInjection.InjectDriversAsync(
                    targetVolumePath, options.DriverPath, options.Recurse, ct);
            }

            sw.Stop();
            return new RestoreResult
            {
                Success = true,
                Duration = sw.Elapsed,
                DriverResult = driverResult
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new RestoreResult
            {
                Success = false,
                Duration = sw.Elapsed,
                Error = "Operation was cancelled."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Restore failed");
            return new RestoreResult
            {
                Success = false,
                Duration = sw.Elapsed,
                Error = ex.Message
            };
        }
    }
}
