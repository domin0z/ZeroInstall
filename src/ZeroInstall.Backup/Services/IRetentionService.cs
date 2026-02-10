using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Manages retention policy and NAS storage usage.
/// </summary>
internal interface IRetentionService
{
    /// <summary>
    /// Calculates the total bytes used by a customer's backups on NAS.
    /// </summary>
    Task<long> CalculateNasUsageAsync(ISftpClientWrapper client, string customerBasePath, CancellationToken ct = default);

    /// <summary>
    /// Enforces the retention policy by deleting the oldest backup runs beyond the keep limit.
    /// Returns the number of runs deleted.
    /// </summary>
    Task<int> EnforceRetentionAsync(
        ISftpClientWrapper client,
        string backupRunsPath,
        int keepLast,
        CancellationToken ct = default);

    /// <summary>
    /// Lists backup run directories sorted by name (oldest first).
    /// </summary>
    Task<List<string>> ListBackupRunsAsync(ISftpClientWrapper client, string backupRunsPath, CancellationToken ct = default);
}
