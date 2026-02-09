using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Services;

/// <summary>
/// Orchestrates capture or restore operations using Core services, reading config from <see cref="ISessionState"/>.
/// </summary>
public interface IMigrationCoordinator
{
    Task CaptureAsync(
        IProgress<TransferProgress>? progress = null,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);

    Task RestoreAsync(
        IProgress<TransferProgress>? progress = null,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default);
}
