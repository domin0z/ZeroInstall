using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Abstracts data movement between source and destination machines.
/// Implementations: ExternalStorageTransport, NetworkShareTransport, DirectWiFiTransport.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Tests whether the transport connection is available and writable.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a stream of data to the destination with metadata.
    /// </summary>
    Task SendAsync(
        Stream data,
        TransferMetadata metadata,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Receives a stream of data from the source based on metadata.
    /// </summary>
    Task<Stream> ReceiveAsync(
        TransferMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Sends the transfer manifest to the destination.
    /// </summary>
    Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default);

    /// <summary>
    /// Receives the transfer manifest from the source.
    /// </summary>
    Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default);
}

/// <summary>
/// Metadata about a file or data chunk being transferred.
/// </summary>
public class TransferMetadata
{
    /// <summary>
    /// Relative path or identifier for this data within the transfer.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// SHA-256 checksum for integrity verification.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Whether this data is compressed.
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Chunk index for resumable transfers (0-based).
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Total number of chunks.
    /// </summary>
    public int TotalChunks { get; set; } = 1;
}
