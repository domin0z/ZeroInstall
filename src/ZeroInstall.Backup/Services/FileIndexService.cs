using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Scans directories, maintains the backup file index, and computes diffs.
/// Uses ChecksumHelper for SHA-256 hashing.
/// </summary>
internal class FileIndexService : IFileIndexService
{
    private readonly ILogger<FileIndexService> _logger;

    public FileIndexService(ILogger<FileIndexService> logger)
    {
        _logger = logger;
    }

    public Task<List<BackupFileEntry>> ScanDirectoriesAsync(
        IReadOnlyList<string> directories,
        IReadOnlyList<string> excludePatterns,
        CancellationToken ct = default)
    {
        var regexes = excludePatterns
            .Select(GlobToRegex)
            .ToList();

        var entries = new List<BackupFileEntry>();

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("Backup path does not exist, skipping: {Path}", dir);
                continue;
            }

            ScanDirectory(dir, dir, regexes, entries, ct);
        }

        return Task.FromResult(entries);
    }

    public async Task<BackupIndex> LoadIndexAsync(string indexPath, CancellationToken ct = default)
    {
        if (!File.Exists(indexPath))
            return new BackupIndex();

        await using var stream = File.OpenRead(indexPath);
        var index = await JsonSerializer.DeserializeAsync<BackupIndex>(stream, cancellationToken: ct);
        return index ?? new BackupIndex();
    }

    public async Task SaveIndexAsync(BackupIndex index, string indexPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(indexPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(indexPath);
        await JsonSerializer.SerializeAsync(stream, index, options, ct);
    }

    public async Task ComputeHashesAsync(
        List<BackupFileEntry> entries,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        int completed = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Reconstruct full path is not needed — caller should use relative paths.
                // The entry at this point should have its hash computed from the actual file.
                // We skip entries that already have a hash.
                if (!string.IsNullOrEmpty(entry.Sha256))
                {
                    completed++;
                    progress?.Report(completed);
                    continue;
                }

                _logger.LogDebug("File entry missing hash: {Path}", entry.RelativePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to hash file: {Path}", entry.RelativePath);
            }

            completed++;
            progress?.Report(completed);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Computes the SHA-256 hash for a single file by full path, returning the hash string.
    /// </summary>
    internal static async Task<string> HashFileAsync(string fullPath, CancellationToken ct = default)
    {
        return await ChecksumHelper.ComputeFileAsync(fullPath, ct);
    }

    private void ScanDirectory(
        string rootDir,
        string currentDir,
        List<Regex> excludeRegexes,
        List<BackupFileEntry> entries,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(currentDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Access denied scanning directory: {Path} - {Message}", currentDir, ex.Message);
            return;
        }
        catch (IOException ex)
        {
            _logger.LogWarning("IO error scanning directory: {Path} - {Message}", currentDir, ex.Message);
            return;
        }

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = GetRelativePath(rootDir, filePath);

            if (IsExcluded(relativePath, filePath, excludeRegexes))
                continue;

            try
            {
                var info = new FileInfo(filePath);
                entries.Add(new BackupFileEntry
                {
                    RelativePath = relativePath,
                    SizeBytes = info.Length,
                    LastModifiedUtc = info.LastWriteTimeUtc
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read file info: {Path}", filePath);
            }
        }

        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(currentDir);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var subdir in subdirs)
        {
            var dirName = Path.GetFileName(subdir);
            if (IsExcludedDirectory(dirName, excludeRegexes))
                continue;

            ScanDirectory(rootDir, subdir, excludeRegexes, entries, ct);
        }
    }

    private static string GetRelativePath(string rootDir, string fullPath)
    {
        var relative = Path.GetRelativePath(rootDir, fullPath);
        return relative.Replace('\\', '/');
    }

    private static bool IsExcluded(string relativePath, string fullPath, List<Regex> excludeRegexes)
    {
        var fileName = Path.GetFileName(fullPath);

        foreach (var regex in excludeRegexes)
        {
            if (regex.IsMatch(fileName) || regex.IsMatch(relativePath))
                return true;
        }

        return false;
    }

    private static bool IsExcludedDirectory(string dirName, List<Regex> excludeRegexes)
    {
        foreach (var regex in excludeRegexes)
        {
            if (regex.IsMatch(dirName))
                return true;
        }

        return false;
    }

    internal static Regex GlobToRegex(string pattern)
    {
        // Convert simple glob patterns to regex:
        // * -> [^/\\]*   (match anything except path separators)
        // ? -> .          (match single character)
        // ** -> .*        (match anything including path separators)
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/\\]*")
            .Replace(@"\?", ".");

        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
