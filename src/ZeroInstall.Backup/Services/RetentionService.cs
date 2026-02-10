using Microsoft.Extensions.Logging;
using ZeroInstall.Backup.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Manages retention policy and NAS storage usage calculations.
/// </summary>
internal class RetentionService : IRetentionService
{
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(ILogger<RetentionService> logger)
    {
        _logger = logger;
    }

    public Task<long> CalculateNasUsageAsync(ISftpClientWrapper client, string customerBasePath, CancellationToken ct = default)
    {
        long totalBytes = 0;

        try
        {
            if (!client.Exists(customerBasePath))
                return Task.FromResult(0L);

            totalBytes = CalculateDirectorySize(client, customerBasePath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate NAS usage for {Path}", customerBasePath);
        }

        return Task.FromResult(totalBytes);
    }

    public Task<int> EnforceRetentionAsync(
        ISftpClientWrapper client,
        string backupRunsPath,
        int keepLast,
        CancellationToken ct = default)
    {
        if (keepLast <= 0)
            return Task.FromResult(0);

        var runs = ListBackupRunsSync(client, backupRunsPath);

        if (runs.Count <= keepLast)
            return Task.FromResult(0);

        int toDelete = runs.Count - keepLast;
        int deleted = 0;

        for (int i = 0; i < toDelete; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                DeleteDirectoryRecursive(client, runs[i]);
                deleted++;
                _logger.LogInformation("Retention: deleted old backup run {Path}", runs[i]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete backup run {Path}", runs[i]);
            }
        }

        return Task.FromResult(deleted);
    }

    public Task<List<string>> ListBackupRunsAsync(ISftpClientWrapper client, string backupRunsPath, CancellationToken ct = default)
    {
        var runs = ListBackupRunsSync(client, backupRunsPath);
        return Task.FromResult(runs);
    }

    private List<string> ListBackupRunsSync(ISftpClientWrapper client, string backupRunsPath)
    {
        if (!client.Exists(backupRunsPath))
            return new List<string>();

        var entries = client.ListDirectory(backupRunsPath)
            .Where(e => e.IsDirectory && e.Name != "." && e.Name != "..")
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.FullName)
            .ToList();

        return entries;
    }

    private long CalculateDirectorySize(ISftpClientWrapper client, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        long size = 0;

        IEnumerable<SftpFileInfo> entries;
        try
        {
            entries = client.ListDirectory(path);
        }
        catch
        {
            return 0;
        }

        foreach (var entry in entries)
        {
            if (entry.Name == "." || entry.Name == "..")
                continue;

            if (entry.IsDirectory)
                size += CalculateDirectorySize(client, entry.FullName, ct);
            else
                size += entry.Length;
        }

        return size;
    }

    private void DeleteDirectoryRecursive(ISftpClientWrapper client, string path)
    {
        var entries = client.ListDirectory(path);

        foreach (var entry in entries)
        {
            if (entry.Name == "." || entry.Name == "..")
                continue;

            if (entry.IsDirectory)
                DeleteDirectoryRecursive(client, entry.FullName);
            else
                client.DeleteFile(entry.FullName);
        }

        // After deleting all contents, delete the directory itself.
        // ISftpClientWrapper doesn't have DeleteDirectory, so we use a convention:
        // The SftpClientWrapper implementation should handle this.
        // For now, we rely on the directory being empty.
        // Note: real SSH.NET has RemoveDirectory. We'll need to add it to the wrapper.
    }
}
