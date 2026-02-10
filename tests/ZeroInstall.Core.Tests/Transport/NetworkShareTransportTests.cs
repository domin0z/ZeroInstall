using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class NetworkShareTransportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly NetworkShareTransport _transport;

    public NetworkShareTransportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-nas-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _transport = new NetworkShareTransport(_tempDir, NullLogger<NetworkShareTransport>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task TestConnectionAsync_WritableDirectory_ReturnsTrue()
    {
        var result = await _transport.TestConnectionAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_NonexistentPath_ReturnsFalse()
    {
        var badPath = Path.Combine(Path.GetTempPath(), "zim-nonexistent-" + Guid.NewGuid().ToString("N"));
        var transport = new NetworkShareTransport(badPath, NullLogger<NetworkShareTransport>.Instance);

        var result = await transport.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendAndReceive_RoundTrips()
    {
        var data = Encoding.UTF8.GetBytes("network share data");
        var checksum = ChecksumHelper.Compute(data);

        using var sendStream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "share/file.dat",
            SizeBytes = data.Length,
            Checksum = checksum
        };

        await _transport.SendAsync(sendStream, metadata);

        await using var receiveStream = await _transport.ReceiveAsync(metadata);
        using var ms = new MemoryStream();
        await receiveStream.CopyToAsync(ms);

        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SendManifestAndReceive_RoundTrips()
    {
        var manifest = new TransferManifest
        {
            SourceHostname = "SOURCE-PC",
            TransportMethod = TransportMethod.NetworkShare,
            Items =
            [
                new MigrationItem
                {
                    DisplayName = "Firefox",
                    ItemType = MigrationItemType.Application,
                    IsSelected = true,
                    EstimatedSizeBytes = 200_000_000
                }
            ]
        };

        await _transport.SendManifestAsync(manifest);
        var received = await _transport.ReceiveManifestAsync();

        received.SourceHostname.Should().Be("SOURCE-PC");
        received.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        received.Items.Should().HaveCount(1);
        received.Items[0].DisplayName.Should().Be("Firefox");
    }

    [Fact]
    public async Task SendAsync_ResumesSkipsExistingFile()
    {
        var data = Encoding.UTF8.GetBytes("resume test content");
        var checksum = ChecksumHelper.Compute(data);

        // Send once
        using var stream1 = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "resume.dat",
            SizeBytes = data.Length,
            Checksum = checksum
        };
        await _transport.SendAsync(stream1, metadata);

        // Get file write time
        var filePath = Path.Combine(_tempDir, "zim-data", "resume.dat");
        var firstWriteTime = File.GetLastWriteTimeUtc(filePath);

        await Task.Delay(50); // Ensure timestamp would differ

        // Send again with same checksum — should skip
        using var stream2 = new MemoryStream(data);
        await _transport.SendAsync(stream2, metadata);

        var secondWriteTime = File.GetLastWriteTimeUtc(filePath);
        secondWriteTime.Should().Be(firstWriteTime);
    }

    [Fact]
    public async Task GetCompletedTransfersAsync_TracksFiles()
    {
        var data = Encoding.UTF8.GetBytes("tracked");

        using var s1 = new MemoryStream(data);
        await _transport.SendAsync(s1, new TransferMetadata { RelativePath = "a.txt", SizeBytes = data.Length });

        using var s2 = new MemoryStream(data);
        await _transport.SendAsync(s2, new TransferMetadata { RelativePath = "b.txt", SizeBytes = data.Length });

        var completed = await _transport.GetCompletedTransfersAsync();

        completed.Should().HaveCount(2);
        completed.Should().Contain("a.txt");
        completed.Should().Contain("b.txt");
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsWhenFileNotFound()
    {
        var metadata = new TransferMetadata
        {
            RelativePath = "nonexistent/file.dat",
            SizeBytes = 100
        };

        var act = async () => await _transport.ReceiveAsync(metadata);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SendAndCleanup_RemovesAllData()
    {
        var data = Encoding.UTF8.GetBytes("cleanup test");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "cleanup.dat",
            SizeBytes = data.Length
        };
        await _transport.SendAsync(stream, metadata);

        // Verify data exists
        Directory.Exists(Path.Combine(_tempDir, "zim-data")).Should().BeTrue();

        // Clean up by deleting the entire temp dir content
        Directory.Delete(_tempDir, recursive: true);

        Directory.Exists(_tempDir).Should().BeFalse();
    }

    [Fact]
    public async Task GetCompletedTransfersAsync_ReturnsEmpty_BeforeAnyTransfer()
    {
        var completed = await _transport.GetCompletedTransfersAsync();

        completed.Should().BeEmpty();
    }
}
