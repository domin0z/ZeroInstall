using ZeroInstall.Core.Models;

namespace ZeroInstall.Backup.Models;

/// <summary>
/// Per-customer backup configuration stored locally and optionally synced from NAS.
/// </summary>
public class BackupConfiguration
{
    /// <summary>
    /// Unique customer identifier (used as NAS folder name).
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable customer/computer name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// SFTP connection settings for the company NAS.
    /// </summary>
    public SftpTransportConfiguration NasConnection { get; set; } = new();

    /// <summary>
    /// Local directories to back up (e.g., C:\Users\Customer\Documents).
    /// </summary>
    public List<string> BackupPaths { get; set; } = new();

    /// <summary>
    /// Glob patterns for files/directories to exclude (e.g., "*.tmp", "node_modules").
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>
    /// Cron expression for the file backup schedule (5-field standard cron).
    /// Default: daily at 2:00 AM.
    /// </summary>
    public string FileBackupCron { get; set; } = "0 2 * * *";

    /// <summary>
    /// Whether to perform periodic full disk image backups.
    /// </summary>
    public bool EnableFullImageBackup { get; set; }

    /// <summary>
    /// Cron expression for full image backup schedule.
    /// Default: first day of every month at 3:00 AM.
    /// </summary>
    public string FullImageCron { get; set; } = "0 3 1 * *";

    /// <summary>
    /// Volume letter to clone for full image backups (e.g., "C").
    /// </summary>
    public string FullImageVolume { get; set; } = "C";

    /// <summary>
    /// Passphrase for AES-256 encryption of backup data. Null/empty = no encryption.
    /// </summary>
    public string? EncryptionPassphrase { get; set; }

    /// <summary>
    /// Whether to compress files before upload.
    /// </summary>
    public bool CompressBeforeUpload { get; set; } = true;

    /// <summary>
    /// Retention policy for old backups.
    /// </summary>
    public RetentionPolicy Retention { get; set; } = new();

    /// <summary>
    /// Per-customer storage quota in bytes. 0 = unlimited.
    /// </summary>
    public long QuotaBytes { get; set; }

    /// <summary>
    /// Interval in minutes for polling NAS for config updates.
    /// </summary>
    public int ConfigSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// UTC timestamp of when this configuration was last modified.
    /// Used for NAS-based config sync (newer wins).
    /// </summary>
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the NAS base path for this customer's backups.
    /// </summary>
    public string GetNasCustomerPath()
    {
        var basePath = NasConnection.RemoteBasePath.TrimEnd('/');
        return $"{basePath}/customers/{CustomerId}";
    }

    /// <summary>
    /// Gets the NAS path for file backup data.
    /// </summary>
    public string GetNasFileBackupPath()
    {
        return $"{GetNasCustomerPath()}/data/file-backups";
    }

    /// <summary>
    /// Gets the NAS path for full image data.
    /// </summary>
    public string GetNasFullImagePath()
    {
        return $"{GetNasCustomerPath()}/data/full-images";
    }

    /// <summary>
    /// Gets the NAS path for config files.
    /// </summary>
    public string GetNasConfigPath()
    {
        return $"{GetNasCustomerPath()}/config";
    }

    /// <summary>
    /// Gets the NAS path for status files.
    /// </summary>
    public string GetNasStatusPath()
    {
        return $"{GetNasCustomerPath()}/status";
    }
}
