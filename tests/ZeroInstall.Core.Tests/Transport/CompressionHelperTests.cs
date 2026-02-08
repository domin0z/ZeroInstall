using System.Text;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class CompressionHelperTests
{
    [Fact]
    public async Task CompressAndDecompress_RoundTrips()
    {
        var original = Encoding.UTF8.GetBytes("Hello, ZeroInstall! This is test data for compression.");

        var compressed = await CompressionHelper.CompressBytesAsync(original);
        var decompressed = await CompressionHelper.DecompressBytesAsync(compressed);

        decompressed.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task CompressBytesAsync_ProducesSmallOutput_ForRepetitiveData()
    {
        // Highly repetitive data should compress well
        var original = Encoding.UTF8.GetBytes(new string('A', 10000));

        var compressed = await CompressionHelper.CompressBytesAsync(original);

        compressed.Length.Should().BeLessThan(original.Length);
    }

    [Fact]
    public async Task CompressAsync_StreamRoundTrips()
    {
        var original = Encoding.UTF8.GetBytes("Stream compression test data with enough content to be meaningful.");
        using var input = new MemoryStream(original);
        using var compressed = new MemoryStream();
        using var decompressed = new MemoryStream();

        await CompressionHelper.CompressAsync(input, compressed);
        compressed.Position = 0;
        await CompressionHelper.DecompressAsync(compressed, decompressed);

        decompressed.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task CompressBytesAsync_EmptyInput_RoundTrips()
    {
        var original = Array.Empty<byte>();

        var compressed = await CompressionHelper.CompressBytesAsync(original);
        var decompressed = await CompressionHelper.DecompressBytesAsync(compressed);

        decompressed.Should().BeEmpty();
    }

    [Fact]
    public async Task CompressBytesAsync_LargeData_RoundTrips()
    {
        // 1 MB of pseudo-random data
        var random = new Random(42);
        var original = new byte[1024 * 1024];
        random.NextBytes(original);

        var compressed = await CompressionHelper.CompressBytesAsync(original);
        var decompressed = await CompressionHelper.DecompressBytesAsync(compressed);

        decompressed.Should().BeEquivalentTo(original);
    }
}
