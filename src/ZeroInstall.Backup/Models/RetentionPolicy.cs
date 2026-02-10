namespace ZeroInstall.Backup.Models;

/// <summary>
/// Retention policy for backup runs on NAS.
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Maximum number of file backup runs to keep. 0 = unlimited.
    /// </summary>
    public int KeepLastFileBackups { get; set; } = 30;

    /// <summary>
    /// Maximum number of full image backups to keep. 0 = unlimited.
    /// </summary>
    public int KeepLastFullImages { get; set; } = 3;
}
