namespace ZeroInstall.Core.Models;

/// <summary>
/// Progress information for an ongoing transfer, reported via IProgress&lt;TransferProgress&gt;.
/// </summary>
public class TransferProgress
{
    /// <summary>
    /// The item currently being transferred.
    /// </summary>
    public string CurrentItemName { get; set; } = string.Empty;

    /// <summary>
    /// Index of the current item (1-based).
    /// </summary>
    public int CurrentItemIndex { get; set; }

    /// <summary>
    /// Total number of items to transfer.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Bytes transferred so far for the current item.
    /// </summary>
    public long CurrentItemBytesTransferred { get; set; }

    /// <summary>
    /// Total bytes for the current item.
    /// </summary>
    public long CurrentItemTotalBytes { get; set; }

    /// <summary>
    /// Total bytes transferred across all items.
    /// </summary>
    public long OverallBytesTransferred { get; set; }

    /// <summary>
    /// Total bytes across all items.
    /// </summary>
    public long OverallTotalBytes { get; set; }

    /// <summary>
    /// Current transfer speed in bytes per second.
    /// </summary>
    public long BytesPerSecond { get; set; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Overall percentage (0.0 to 1.0).
    /// </summary>
    public double OverallPercentage => OverallTotalBytes > 0
        ? (double)OverallBytesTransferred / OverallTotalBytes
        : 0;
}
