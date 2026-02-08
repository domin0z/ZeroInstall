using System.Text;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class ChecksumHelperTests
{
    [Fact]
    public async Task ComputeAsync_ReturnsConsistentHash()
    {
        var data = Encoding.UTF8.GetBytes("Hello, ZeroInstall!");
        using var stream = new MemoryStream(data);

        var hash1 = await ChecksumHelper.ComputeAsync(stream);
        stream.Position = 0;
        var hash2 = await ChecksumHelper.ComputeAsync(stream);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64); // SHA-256 = 64 hex chars
        hash1.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public async Task ComputeAsync_ResetsStreamPosition()
    {
        var data = Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(data);
        stream.Position = 0;

        await ChecksumHelper.ComputeAsync(stream);

        stream.Position.Should().Be(0);
    }

    [Fact]
    public void Compute_ByteArray_ReturnsCorrectHash()
    {
        var data = Encoding.UTF8.GetBytes("Hello, ZeroInstall!");

        var hash = ChecksumHelper.Compute(data);

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public void Compute_DifferentData_ReturnsDifferentHash()
    {
        var hash1 = ChecksumHelper.Compute(Encoding.UTF8.GetBytes("data1"));
        var hash2 = ChecksumHelper.Compute(Encoding.UTF8.GetBytes("data2"));

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task ComputeFileAsync_ReturnsCorrectHash()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = Encoding.UTF8.GetBytes("file content for hashing");
            await File.WriteAllBytesAsync(tempFile, data);

            var fileHash = await ChecksumHelper.ComputeFileAsync(tempFile);
            var directHash = ChecksumHelper.Compute(data);

            fileHash.Should().Be(directHash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyFileAsync_MatchingChecksum_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = Encoding.UTF8.GetBytes("verify me");
            await File.WriteAllBytesAsync(tempFile, data);
            var expected = ChecksumHelper.Compute(data);

            var result = await ChecksumHelper.VerifyFileAsync(tempFile, expected);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task VerifyFileAsync_MismatchedChecksum_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, Encoding.UTF8.GetBytes("actual data"));

            var result = await ChecksumHelper.VerifyFileAsync(tempFile, "0000000000000000000000000000000000000000000000000000000000000000");

            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
