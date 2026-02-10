namespace ZeroInstall.Backup.Models;

/// <summary>
/// Tracks a single file in the backup index.
/// </summary>
public class BackupFileEntry
{
    /// <summary>
    /// Relative path from the backup root (forward-slash separated).
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Last modified time (UTC) from the filesystem.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// SHA-256 hash of the file content (lowercase hex).
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of when this file was last backed up.
    /// </summary>
    public DateTime? BackedUpUtc { get; set; }

    /// <summary>
    /// The backup run ID that last uploaded this file.
    /// </summary>
    public string? BackupRunId { get; set; }
}
