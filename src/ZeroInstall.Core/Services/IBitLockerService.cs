using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Service for querying and managing BitLocker encryption on volumes.
/// </summary>
public interface IBitLockerService
{
    /// <summary>
    /// Gets the BitLocker status for a volume.
    /// </summary>
    /// <param name="volumePath">Volume path (e.g., "C:" or "C:\").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed BitLocker status, or a status with <see cref="Enums.BitLockerProtectionStatus.Unknown"/> on failure.</returns>
    Task<BitLockerStatus> GetStatusAsync(string volumePath, CancellationToken ct = default);

    /// <summary>
    /// Unlocks a BitLocker-locked volume using a recovery password or key file.
    /// </summary>
    /// <param name="volumePath">Volume path (e.g., "D:").</param>
    /// <param name="recoveryPassword">48-digit recovery password (e.g., "123456-789012-345678-901234-567890-123456-789012-345678"), or null to use key file.</param>
    /// <param name="recoveryKeyPath">Path to a .bek recovery key file, or null to use password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if unlock succeeded.</returns>
    Task<bool> UnlockVolumeAsync(string volumePath, string? recoveryPassword = null, string? recoveryKeyPath = null, CancellationToken ct = default);

    /// <summary>
    /// Suspends BitLocker protection on a volume. Data remains encrypted on disk but the key
    /// is stored in the clear, allowing safe reads. Recommended before cloning.
    /// </summary>
    /// <param name="volumePath">Volume path (e.g., "C:").</param>
    /// <param name="rebootCount">Number of reboots to stay suspended (0 = indefinite until resumed).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if suspension succeeded.</returns>
    Task<bool> SuspendProtectionAsync(string volumePath, int rebootCount = 0, CancellationToken ct = default);

    /// <summary>
    /// Resumes BitLocker protection on a volume after suspension.
    /// </summary>
    /// <param name="volumePath">Volume path (e.g., "C:").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if resuming succeeded.</returns>
    Task<bool> ResumeProtectionAsync(string volumePath, CancellationToken ct = default);
}
