namespace ZeroInstall.Backup.Models;

/// <summary>
/// Full file index for incremental backup tracking.
/// Persisted as backup-index.json alongside the application.
/// </summary>
public class BackupIndex
{
    /// <summary>
    /// Customer identifier this index belongs to.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the last full directory scan.
    /// </summary>
    public DateTime? LastScanUtc { get; set; }

    /// <summary>
    /// All tracked files in the index.
    /// </summary>
    public List<BackupFileEntry> Files { get; set; } = new();

    /// <summary>
    /// Computes the set of files that have changed compared to a new scan result.
    /// A file is considered changed if it is new, has a different size, or a different hash.
    /// </summary>
    public List<BackupFileEntry> GetChangedFiles(List<BackupFileEntry> currentFiles)
    {
        var indexed = new Dictionary<string, BackupFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Files)
        {
            indexed[entry.RelativePath] = entry;
        }

        var changed = new List<BackupFileEntry>();
        foreach (var current in currentFiles)
        {
            if (!indexed.TryGetValue(current.RelativePath, out var existing))
            {
                // New file
                changed.Add(current);
            }
            else if (existing.SizeBytes != current.SizeBytes ||
                     !string.Equals(existing.Sha256, current.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                // Modified file
                changed.Add(current);
            }
        }

        return changed;
    }

    /// <summary>
    /// Computes the set of files that were in the index but no longer exist on disk.
    /// </summary>
    public List<BackupFileEntry> GetDeletedFiles(List<BackupFileEntry> currentFiles)
    {
        var currentPaths = new HashSet<string>(
            currentFiles.Select(f => f.RelativePath),
            StringComparer.OrdinalIgnoreCase);

        return Files.Where(f => !currentPaths.Contains(f.RelativePath)).ToList();
    }
}
