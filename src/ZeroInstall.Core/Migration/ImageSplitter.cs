using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Splits large disk image files into chunks for FAT32 compatibility (4 GB max per file)
/// and reassembles them on the destination.
/// </summary>
public static class ImageSplitter
{
    /// <summary>
    /// Maximum chunk size for FAT32 file systems (4 GB - 1 byte).
    /// </summary>
    public const long Fat32MaxFileSize = 4L * 1024 * 1024 * 1024 - 1;

    /// <summary>
    /// Default chunk size (slightly under FAT32 limit).
    /// </summary>
    public const long DefaultChunkSize = 4L * 1024 * 1024 * 1024 - 4096;

    /// <summary>
    /// Determines whether a file needs to be split for the target filesystem.
    /// </summary>
    public static bool NeedsSplitting(long fileSizeBytes, long maxChunkSize = Fat32MaxFileSize)
    {
        return fileSizeBytes > maxChunkSize;
    }

    /// <summary>
    /// Calculates how many chunks a file will be split into.
    /// </summary>
    public static int CalculateChunkCount(long fileSizeBytes, long chunkSize = DefaultChunkSize)
    {
        if (fileSizeBytes <= 0) return 1;
        return (int)((fileSizeBytes + chunkSize - 1) / chunkSize);
    }

    /// <summary>
    /// Splits a file into chunks.
    /// </summary>
    public static async Task<List<string>> SplitAsync(
        string sourceFilePath,
        long chunkSize = DefaultChunkSize,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(sourceFilePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("Source image file not found", sourceFilePath);

        var totalSize = fileInfo.Length;
        var chunkCount = CalculateChunkCount(totalSize, chunkSize);
        var chunkPaths = new List<string>();
        var buffer = new byte[81920]; // 80 KB buffer
        long totalBytesWritten = 0;

        await using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 81920, useAsync: true);

        for (int i = 0; i < chunkCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var chunkPath = DiskImageMetadata.GetChunkPath(sourceFilePath, i);
            chunkPaths.Add(chunkPath);

            var bytesRemaining = Math.Min(chunkSize, totalSize - totalBytesWritten);

            await using var chunkStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true);

            long chunkBytesWritten = 0;
            while (chunkBytesWritten < bytesRemaining)
            {
                var toRead = (int)Math.Min(buffer.Length, bytesRemaining - chunkBytesWritten);
                var bytesRead = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);
                if (bytesRead == 0) break;

                await chunkStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                chunkBytesWritten += bytesRead;
                totalBytesWritten += bytesRead;

                progress?.Report(new TransferProgress
                {
                    CurrentItemName = $"Splitting: chunk {i + 1}/{chunkCount}",
                    CurrentItemIndex = i + 1,
                    TotalItems = chunkCount,
                    CurrentItemBytesTransferred = chunkBytesWritten,
                    CurrentItemTotalBytes = bytesRemaining,
                    OverallBytesTransferred = totalBytesWritten,
                    OverallTotalBytes = totalSize
                });
            }
        }

        return chunkPaths;
    }

    /// <summary>
    /// Reassembles chunk files into a single image file.
    /// </summary>
    public static async Task ReassembleAsync(
        string outputFilePath,
        int chunkCount,
        string baseChunkPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var buffer = new byte[81920];
        long totalBytesWritten = 0;

        // Calculate total size from chunks
        long totalSize = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            var chunkPath = DiskImageMetadata.GetChunkPath(baseChunkPath, i);
            if (!File.Exists(chunkPath))
                throw new FileNotFoundException($"Chunk file not found: {chunkPath}", chunkPath);
            totalSize += new FileInfo(chunkPath).Length;
        }

        await using var output = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 81920, useAsync: true);

        for (int i = 0; i < chunkCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var chunkPath = DiskImageMetadata.GetChunkPath(baseChunkPath, i);
            await using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 81920, useAsync: true);

            int bytesRead;
            while ((bytesRead = await chunkStream.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalBytesWritten += bytesRead;

                progress?.Report(new TransferProgress
                {
                    CurrentItemName = $"Reassembling: chunk {i + 1}/{chunkCount}",
                    CurrentItemIndex = i + 1,
                    TotalItems = chunkCount,
                    OverallBytesTransferred = totalBytesWritten,
                    OverallTotalBytes = totalSize
                });
            }
        }
    }

    /// <summary>
    /// Checks if a target path is on a FAT32 filesystem.
    /// </summary>
    public static bool IsFat32(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (root is null) return false;
            var drive = new DriveInfo(root);
            return drive.IsReady &&
                   string.Equals(drive.DriveFormat, "FAT32", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
