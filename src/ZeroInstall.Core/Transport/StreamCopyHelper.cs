using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Copies streams with progress reporting, cancellation, and optional bandwidth throttling.
/// </summary>
public static class StreamCopyHelper
{
    private const int DefaultBufferSize = 81920; // 80 KB

    /// <summary>
    /// Copies from source to destination with progress reporting and optional throttling.
    /// </summary>
    public static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        string itemName,
        int itemIndex,
        int totalItems,
        long overallBytesAlreadyTransferred,
        long overallTotalBytes,
        IProgress<TransferProgress>? progress,
        int? maxBytesPerSecond = null,
        CancellationToken ct = default)
    {
        var buffer = new byte[DefaultBufferSize];
        long itemBytesTransferred = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long bytesInCurrentSecond = 0;
        var lastSecondStart = stopwatch.ElapsedMilliseconds;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, ct)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            itemBytesTransferred += bytesRead;
            bytesInCurrentSecond += bytesRead;

            // Progress reporting
            var elapsed = stopwatch.ElapsedMilliseconds;
            var bytesPerSecond = elapsed > 0 ? itemBytesTransferred * 1000 / elapsed : 0;
            var remaining = bytesPerSecond > 0 && totalBytes > itemBytesTransferred
                ? TimeSpan.FromSeconds((double)(overallTotalBytes - overallBytesAlreadyTransferred - itemBytesTransferred) / bytesPerSecond)
                : (TimeSpan?)null;

            progress?.Report(new TransferProgress
            {
                CurrentItemName = itemName,
                CurrentItemIndex = itemIndex,
                TotalItems = totalItems,
                CurrentItemBytesTransferred = itemBytesTransferred,
                CurrentItemTotalBytes = totalBytes,
                OverallBytesTransferred = overallBytesAlreadyTransferred + itemBytesTransferred,
                OverallTotalBytes = overallTotalBytes,
                BytesPerSecond = bytesPerSecond,
                EstimatedTimeRemaining = remaining
            });

            // Bandwidth throttling
            if (maxBytesPerSecond.HasValue && maxBytesPerSecond.Value > 0)
            {
                var currentSecondElapsed = elapsed - lastSecondStart;
                if (bytesInCurrentSecond >= maxBytesPerSecond.Value)
                {
                    var delayMs = 1000 - currentSecondElapsed;
                    if (delayMs > 0)
                        await Task.Delay((int)delayMs, ct);
                    bytesInCurrentSecond = 0;
                    lastSecondStart = stopwatch.ElapsedMilliseconds;
                }
            }
        }
    }
}
