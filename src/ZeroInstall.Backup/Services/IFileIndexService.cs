using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Scans directories, maintains the backup file index, and computes diffs.
/// </summary>
internal interface IFileIndexService
{
    /// <summary>
    /// Scans the specified directories and returns file entries for all files found,
    /// excluding files matching the exclude patterns.
    /// </summary>
    Task<List<BackupFileEntry>> ScanDirectoriesAsync(
        IReadOnlyList<string> directories,
        IReadOnlyList<string> excludePatterns,
        CancellationToken ct = default);

    /// <summary>
    /// Loads the backup index from the specified JSON file path.
    /// Returns a new empty index if the file doesn't exist.
    /// </summary>
    Task<BackupIndex> LoadIndexAsync(string indexPath, CancellationToken ct = default);

    /// <summary>
    /// Saves the backup index to the specified JSON file path.
    /// </summary>
    Task SaveIndexAsync(BackupIndex index, string indexPath, CancellationToken ct = default);

    /// <summary>
    /// Computes SHA-256 hashes for the given file entries (those that need hashing).
    /// Updates the Sha256 property in place.
    /// </summary>
    Task ComputeHashesAsync(
        List<BackupFileEntry> entries,
        IProgress<int>? progress = null,
        CancellationToken ct = default);
}
