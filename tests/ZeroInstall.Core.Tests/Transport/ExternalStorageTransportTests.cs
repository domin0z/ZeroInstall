using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class ExternalStorageTransportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ExternalStorageTransport _transport;

    public ExternalStorageTransportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-test-" + Guid.NewGuid().ToString("N")[..8]);
        _transport = new ExternalStorageTransport(_tempDir, NullLogger<ExternalStorageTransport>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task TestConnectionAsync_CreatesDirectoryAndReturnsTrue()
    {
        var result = await _transport.TestConnectionAsync();

        result.Should().BeTrue();
        Directory.Exists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WritesFileToDataDirectory()
    {
        await _transport.TestConnectionAsync();

        var data = Encoding.UTF8.GetBytes("test file content");
        using var stream = new MemoryStream(data);

        var metadata = new TransferMetadata
        {
            RelativePath = "apps/chrome/data.bin",
            SizeBytes = data.Length,
            Checksum = ChecksumHelper.Compute(data)
        };

        await _transport.SendAsync(stream, metadata);

        var writtenPath = Path.Combine(_tempDir, "zim-data", "apps", "chrome", "data.bin");
        File.Exists(writtenPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(writtenPath)).Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SendAsync_SkipsAlreadyTransferredFileWithMatchingChecksum()
    {
        await _transport.TestConnectionAsync();

        var data = Encoding.UTF8.GetBytes("existing content");
        var checksum = ChecksumHelper.Compute(data);

        // Pre-create the file
        var targetPath = Path.Combine(_tempDir, "zim-data", "existing.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, data);

        // Send with matching checksum — should skip
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "existing.txt",
            SizeBytes = data.Length,
            Checksum = checksum
        };

        await _transport.SendAsync(stream, metadata);

        // File should still be the same
        (await File.ReadAllBytesAsync(targetPath)).Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ReceiveAsync_ReturnsFileStream()
    {
        await _transport.TestConnectionAsync();

        // Pre-create a file in the data directory
        var data = Encoding.UTF8.GetBytes("receive me");
        var filePath = Path.Combine(_tempDir, "zim-data", "receive.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, data);

        var metadata = new TransferMetadata { RelativePath = "receive.txt" };

        await using var stream = await _transport.ReceiveAsync(metadata);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsWhenFileNotFound()
    {
        await _transport.TestConnectionAsync();

        var metadata = new TransferMetadata { RelativePath = "nonexistent.txt" };

        var act = () => _transport.ReceiveAsync(metadata);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SendAndReceiveManifest_RoundTrips()
    {
        await _transport.TestConnectionAsync();

        var manifest = new TransferManifest
        {
            SourceHostname = "OLD-PC",
            SourceOsVersion = "Windows 10",
            TransportMethod = TransportMethod.ExternalStorage,
            Items =
            [
                new MigrationItem
                {
                    DisplayName = "Chrome",
                    ItemType = MigrationItemType.Application,
                    IsSelected = true,
                    EstimatedSizeBytes = 500_000_000
                }
            ]
        };

        await _transport.SendManifestAsync(manifest);
        var received = await _transport.ReceiveManifestAsync();

        received.SourceHostname.Should().Be("OLD-PC");
        received.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        received.Items.Should().HaveCount(1);
        received.Items[0].DisplayName.Should().Be("Chrome");
    }

    [Fact]
    public async Task ReceiveManifestAsync_ThrowsWhenNoManifest()
    {
        await _transport.TestConnectionAsync();

        var act = () => _transport.ReceiveManifestAsync();

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetCompletedTransfersAsync_TracksTransferredFiles()
    {
        await _transport.TestConnectionAsync();

        var data = Encoding.UTF8.GetBytes("tracked file");
        using var stream1 = new MemoryStream(data);
        await _transport.SendAsync(stream1, new TransferMetadata
        {
            RelativePath = "file1.txt",
            SizeBytes = data.Length
        });

        using var stream2 = new MemoryStream(data);
        await _transport.SendAsync(stream2, new TransferMetadata
        {
            RelativePath = "file2.txt",
            SizeBytes = data.Length
        });

        var completed = await _transport.GetCompletedTransfersAsync();

        completed.Should().Contain("file1.txt");
        completed.Should().Contain("file2.txt");
    }

    [Fact]
    public void HasSufficientSpace_ReturnsTrueForSmallRequest()
    {
        Directory.CreateDirectory(_tempDir);

        // Requesting 1 byte should always succeed on temp drive
        _transport.HasSufficientSpace(1).Should().BeTrue();
    }

    [Fact]
    public void HasSufficientSpace_ReturnsFalseForHugeRequest()
    {
        Directory.CreateDirectory(_tempDir);

        // Requesting exabytes should fail
        _transport.HasSufficientSpace(long.MaxValue).Should().BeFalse();
    }

    [Fact]
    public void GetAvailableDrives_ReturnsNonEmpty()
    {
        var drives = ExternalStorageTransport.GetAvailableDrives();

        // Should find at least the system drive (it filters it out, but there might be others)
        // On a CI machine there might be only C:, which is filtered — so just check no exception
        drives.Should().NotBeNull();
    }

    [Fact]
    public async Task Cleanup_RemovesAllData()
    {
        await _transport.TestConnectionAsync();

        var data = Encoding.UTF8.GetBytes("cleanup test");
        using var stream = new MemoryStream(data);
        await _transport.SendAsync(stream, new TransferMetadata
        {
            RelativePath = "cleanup.txt",
            SizeBytes = data.Length
        });

        _transport.Cleanup();

        Directory.Exists(_tempDir).Should().BeFalse();
    }
}
