using ZeroInstall.Backup.Enums;

namespace ZeroInstall.Backup.Models;

/// <summary>
/// Result of a single backup run (file or full image).
/// </summary>
public class BackupRunResult
{
    /// <summary>
    /// Unique identifier for this backup run.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Type of backup performed ("file" or "full-image").
    /// </summary>
    public string BackupType { get; set; } = "file";

    /// <summary>
    /// Overall result of the backup run.
    /// </summary>
    public BackupRunResultType ResultType { get; set; }

    /// <summary>
    /// UTC timestamp when the run started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the run completed.
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Total files scanned during this run.
    /// </summary>
    public int FilesScanned { get; set; }

    /// <summary>
    /// Files that were uploaded (new or changed).
    /// </summary>
    public int FilesUploaded { get; set; }

    /// <summary>
    /// Files that failed to upload.
    /// </summary>
    public int FilesFailed { get; set; }

    /// <summary>
    /// Total bytes transferred during this run.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Duration of the backup run.
    /// </summary>
    public TimeSpan Duration => CompletedUtc - StartedUtc;

    /// <summary>
    /// Error messages for files that failed.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
