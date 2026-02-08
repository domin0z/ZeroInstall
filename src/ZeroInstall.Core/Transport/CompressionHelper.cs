using System.IO.Compression;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// GZip compression/decompression utilities for transfer data.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Compresses a stream to a destination stream using GZip.
    /// </summary>
    public static async Task CompressAsync(Stream source, Stream destination, CancellationToken ct = default)
    {
        await using var gzip = new GZipStream(destination, CompressionLevel.Fastest, leaveOpen: true);
        await source.CopyToAsync(gzip, ct);
    }

    /// <summary>
    /// Decompresses a GZip stream to a destination stream.
    /// </summary>
    public static async Task DecompressAsync(Stream source, Stream destination, CancellationToken ct = default)
    {
        await using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        await gzip.CopyToAsync(destination, ct);
    }

    /// <summary>
    /// Compresses data in memory.
    /// </summary>
    public static async Task<byte[]> CompressBytesAsync(byte[] data, CancellationToken ct = default)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        await CompressAsync(input, output, ct);
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses data in memory.
    /// </summary>
    public static async Task<byte[]> DecompressBytesAsync(byte[] compressedData, CancellationToken ct = default)
    {
        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();
        await DecompressAsync(input, output, ct);
        return output.ToArray();
    }
}
