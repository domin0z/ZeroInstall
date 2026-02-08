using System.Security.Cryptography;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// SHA-256 checksum utilities for transfer integrity verification.
/// </summary>
public static class ChecksumHelper
{
    /// <summary>
    /// Computes SHA-256 hash of a stream (resets position to beginning afterward).
    /// </summary>
    public static async Task<string> ComputeAsync(Stream stream, CancellationToken ct = default)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        var hash = await SHA256.HashDataAsync(stream, ct);
        if (stream.CanSeek) stream.Position = position;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA-256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeFileAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await ComputeAsync(stream, ct);
    }

    /// <summary>
    /// Computes SHA-256 hash of a byte array.
    /// </summary>
    public static string Compute(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a file matches the expected checksum.
    /// </summary>
    public static async Task<bool> VerifyFileAsync(string filePath, string expectedChecksum, CancellationToken ct = default)
    {
        var actual = await ComputeFileAsync(filePath, ct);
        return string.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
