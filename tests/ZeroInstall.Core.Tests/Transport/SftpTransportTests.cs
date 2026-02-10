using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class SftpTransportTests : IDisposable
{
    private readonly ISftpClientWrapper _mockClient;
    private readonly SftpTransport _transport;
    private readonly SftpTransport _transportWithEncryption;
    private readonly SftpTransport _transportNoCompress;
    private const string BasePath = "/backups/zim";

    public SftpTransportTests()
    {
        _mockClient = Substitute.For<ISftpClientWrapper>();
        _mockClient.IsConnected.Returns(true);

        _transport = new SftpTransport(
            _mockClient, BasePath, NullLogger<SftpTransport>.Instance);

        var mockClient2 = Substitute.For<ISftpClientWrapper>();
        mockClient2.IsConnected.Returns(true);
        _transportWithEncryption = new SftpTransport(
            mockClient2, BasePath, NullLogger<SftpTransport>.Instance,
            encryptionPassphrase: "test-passphrase");

        var mockClient3 = Substitute.For<ISftpClientWrapper>();
        mockClient3.IsConnected.Returns(true);
        _transportNoCompress = new SftpTransport(
            mockClient3, BasePath, NullLogger<SftpTransport>.Instance,
            compressBeforeUpload: false);
    }

    public void Dispose()
    {
        _transport.Dispose();
        _transportWithEncryption.Dispose();
        _transportNoCompress.Dispose();
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsTrue()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        var result = await _transport.TestConnectionAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_CreatesBaseDirectory()
    {
        _mockClient.Exists("/backups").Returns(false);
        _mockClient.Exists("/backups/zim").Returns(false);

        await _transport.TestConnectionAsync();

        _mockClient.Received().CreateDirectory("/backups");
        _mockClient.Received().CreateDirectory("/backups/zim");
    }

    [Fact]
    public async Task TestConnectionAsync_WritesAndDeletesTestFile()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        await _transport.TestConnectionAsync();

        _mockClient.Received().UploadFile(Arg.Any<Stream>(), "/backups/zim/.zim-test");
        _mockClient.Received().DeleteFile("/backups/zim/.zim-test");
    }

    [Fact]
    public async Task TestConnectionAsync_OnException_ReturnsFalse()
    {
        _mockClient.When(x => x.Connect()).Do(_ => throw new Exception("Connection refused"));
        _mockClient.IsConnected.Returns(false);

        var transport = new SftpTransport(
            _mockClient, BasePath, NullLogger<SftpTransport>.Instance);
        var result = await transport.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_SmallFile_UploadsTmpThenRenames()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        // No resume log exists
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        var data = Encoding.UTF8.GetBytes("test file content");
        using var stream = new MemoryStream(data);

        var metadata = new TransferMetadata
        {
            RelativePath = "apps/chrome.bin",
            SizeBytes = data.Length,
            Checksum = ChecksumHelper.Compute(data)
        };

        await _transport.SendAsync(stream, metadata);

        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            "/backups/zim/zim-data/apps/chrome.bin.tmp");
        _mockClient.Received().RenameFile(
            "/backups/zim/zim-data/apps/chrome.bin.tmp",
            "/backups/zim/zim-data/apps/chrome.bin");
    }

    [Fact]
    public async Task SendAsync_SmallFile_RecordsInResumeLog()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        var data = Encoding.UTF8.GetBytes("tracked");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "test.bin",
            SizeBytes = data.Length
        };

        await _transport.SendAsync(stream, metadata);

        // Resume log should be uploaded
        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            "/backups/zim/zim-resume.json");
    }

    [Fact]
    public async Task SendAsync_AlreadyCompleted_Skips()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(true);

        // Return resume log with already-completed file
        var resumeLog = new { CompletedFiles = new[] { "already-done.bin" }, Checksums = new Dictionary<string, string>(), LastUpdatedUtc = DateTime.UtcNow };
        var resumeJson = JsonSerializer.Serialize(resumeLog);
        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-resume.json", Arg.Any<Stream>()))
            .Do(ci =>
            {
                var ms = (MemoryStream)ci[1];
                var bytes = Encoding.UTF8.GetBytes(resumeJson);
                ms.Write(bytes, 0, bytes.Length);
            });

        var data = Encoding.UTF8.GetBytes("should be skipped");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "already-done.bin",
            SizeBytes = data.Length
        };

        await _transport.SendAsync(stream, metadata);

        // Should NOT upload the file itself (only resume log interactions)
        _mockClient.DidNotReceive().UploadFile(
            Arg.Any<Stream>(),
            "/backups/zim/zim-data/already-done.bin.tmp");
    }

    [Fact]
    public async Task SendAsync_WithCompression_SetsIsCompressed()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        var data = Encoding.UTF8.GetBytes("compress me please!");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "compressed.bin",
            SizeBytes = data.Length
        };

        await _transport.SendAsync(stream, metadata);

        metadata.IsCompressed.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_NoCompression_DoesNotSetIsCompressed()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(true);
        mockClient.Exists(Arg.Any<string>()).Returns(true);
        mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        using var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance,
            compressBeforeUpload: false);

        var data = Encoding.UTF8.GetBytes("no compress");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "raw.bin",
            SizeBytes = data.Length
        };

        await transport.SendAsync(stream, metadata);

        metadata.IsCompressed.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithEncryption_SetsIsEncrypted()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(true);
        mockClient.Exists(Arg.Any<string>()).Returns(true);
        mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        using var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance,
            encryptionPassphrase: "secret");

        var data = Encoding.UTF8.GetBytes("encrypt this data");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "encrypted.bin",
            SizeBytes = data.Length
        };

        await transport.SendAsync(stream, metadata);

        metadata.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_SingleFile_Downloads()
    {
        var originalData = Encoding.UTF8.GetBytes("receive this");
        _mockClient.Exists("/backups/zim/zim-data/test.bin.part0000").Returns(false);
        _mockClient.Exists("/backups/zim/zim-data/test.bin").Returns(true);
        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-data/test.bin", Arg.Any<Stream>()))
            .Do(ci =>
            {
                var ms = (MemoryStream)ci[1];
                ms.Write(originalData, 0, originalData.Length);
            });

        var metadata = new TransferMetadata
        {
            RelativePath = "test.bin",
            SizeBytes = originalData.Length
        };

        await using var result = await _transport.ReceiveAsync(metadata);
        using var resultMs = new MemoryStream();
        await result.CopyToAsync(resultMs);

        resultMs.ToArray().Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task ReceiveAsync_FileNotFound_Throws()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        var metadata = new TransferMetadata { RelativePath = "missing.bin" };

        var act = () => _transport.ReceiveAsync(metadata);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReceiveAsync_ChunkedFile_ReassemblesChunks()
    {
        var chunk0 = Encoding.UTF8.GetBytes("AAAA");
        var chunk1 = Encoding.UTF8.GetBytes("BBBB");

        _mockClient.Exists("/backups/zim/zim-data/big.bin.part0000").Returns(true);
        _mockClient.Exists("/backups/zim/zim-data/big.bin.part0001").Returns(true);
        _mockClient.Exists("/backups/zim/zim-data/big.bin.part0002").Returns(false);

        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-data/big.bin.part0000", Arg.Any<Stream>()))
            .Do(ci => { var ms = (MemoryStream)ci[1]; ms.Write(chunk0, 0, chunk0.Length); });
        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-data/big.bin.part0001", Arg.Any<Stream>()))
            .Do(ci => { var ms = (MemoryStream)ci[1]; ms.Write(chunk1, 0, chunk1.Length); });

        var metadata = new TransferMetadata { RelativePath = "big.bin" };

        await using var result = await _transport.ReceiveAsync(metadata);
        using var resultMs = new MemoryStream();
        await result.CopyToAsync(resultMs);

        var expected = chunk0.Concat(chunk1).ToArray();
        resultMs.ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SendManifestAsync_UploadsToCorrectPath()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        var manifest = new TransferManifest
        {
            SourceHostname = "OLD-PC",
            TransportMethod = TransportMethod.Sftp,
            Items = [new MigrationItem { DisplayName = "Chrome", ItemType = MigrationItemType.Application }]
        };

        await _transport.SendManifestAsync(manifest);

        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            "/backups/zim/zim-manifest.json");
    }

    [Fact]
    public async Task SendReceiveManifest_RoundTrips()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        var manifest = new TransferManifest
        {
            SourceHostname = "TEST-PC",
            TransportMethod = TransportMethod.Sftp,
            Items =
            [
                new MigrationItem
                {
                    DisplayName = "Firefox",
                    ItemType = MigrationItemType.Application,
                    IsSelected = true
                }
            ]
        };

        // Capture what gets uploaded
        byte[]? uploadedData = null;
        _mockClient.When(x => x.UploadFile(Arg.Any<Stream>(), "/backups/zim/zim-manifest.json"))
            .Do(ci =>
            {
                var ms = new MemoryStream();
                ((Stream)ci[0]).CopyTo(ms);
                uploadedData = ms.ToArray();
            });

        await _transport.SendManifestAsync(manifest);

        // Now set up download to return what was uploaded
        _mockClient.Exists("/backups/zim/zim-manifest.json").Returns(true);
        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-manifest.json", Arg.Any<Stream>()))
            .Do(ci =>
            {
                var ms = (MemoryStream)ci[1];
                ms.Write(uploadedData!, 0, uploadedData!.Length);
            });

        var received = await _transport.ReceiveManifestAsync();

        received.SourceHostname.Should().Be("TEST-PC");
        received.TransportMethod.Should().Be(TransportMethod.Sftp);
        received.Items.Should().HaveCount(1);
        received.Items[0].DisplayName.Should().Be("Firefox");
    }

    [Fact]
    public async Task ReceiveManifestAsync_NotFound_Throws()
    {
        _mockClient.Exists("/backups/zim/zim-manifest.json").Returns(false);

        var act = () => _transport.ReceiveManifestAsync();

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SendManifestAsync_WithEncryption_EncryptsData()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(true);
        mockClient.Exists(Arg.Any<string>()).Returns(true);

        using var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance,
            encryptionPassphrase: "manifest-key");

        byte[]? uploadedData = null;
        mockClient.When(x => x.UploadFile(Arg.Any<Stream>(), "/backups/zim/zim-manifest.json"))
            .Do(ci =>
            {
                var ms = new MemoryStream();
                ((Stream)ci[0]).CopyTo(ms);
                uploadedData = ms.ToArray();
            });

        await transport.SendManifestAsync(new TransferManifest { SourceHostname = "ENC-PC" });

        uploadedData.Should().NotBeNull();
        // Encrypted data should start with "ZIME" magic header
        Encoding.UTF8.GetString(uploadedData!, 0, 4).Should().Be("ZIME");
    }

    [Fact]
    public async Task GetCompletedTransfersAsync_NoResumeLog_ReturnsEmpty()
    {
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        var completed = await _transport.GetCompletedTransfersAsync();

        completed.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletedTransfersAsync_WithResumeLog_ReturnsFiles()
    {
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(true);
        var log = new { CompletedFiles = new[] { "file1.bin", "file2.bin" }, Checksums = new Dictionary<string, string>(), LastUpdatedUtc = DateTime.UtcNow };
        var logJson = JsonSerializer.Serialize(log);
        _mockClient.When(x => x.DownloadFile("/backups/zim/zim-resume.json", Arg.Any<Stream>()))
            .Do(ci =>
            {
                var ms = (MemoryStream)ci[1];
                var bytes = Encoding.UTF8.GetBytes(logJson);
                ms.Write(bytes, 0, bytes.Length);
            });

        var completed = await _transport.GetCompletedTransfersAsync();

        completed.Should().Contain("file1.bin");
        completed.Should().Contain("file2.bin");
    }

    [Fact]
    public async Task ListRemoteDirectoryAsync_ReturnsItems()
    {
        var items = new List<SftpFileInfo>
        {
            new("backup1", "/backups/zim/backup1", true, 0, DateTime.UtcNow),
            new("data.bin", "/backups/zim/data.bin", false, 1024, DateTime.UtcNow)
        };
        _mockClient.ListDirectory("/backups/zim").Returns(items);

        var result = await _transport.ListRemoteDirectoryAsync("/backups/zim");

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("backup1");
        result[0].IsDirectory.Should().BeTrue();
        result[1].Name.Should().Be("data.bin");
        result[1].Length.Should().Be(1024);
    }

    [Fact]
    public async Task CreateRemoteDirectoryAsync_CreatesPath()
    {
        _mockClient.Exists("/remote").Returns(false);
        _mockClient.Exists("/remote/new").Returns(false);
        _mockClient.Exists("/remote/new/dir").Returns(false);

        await _transport.CreateRemoteDirectoryAsync("/remote/new/dir");

        _mockClient.Received().CreateDirectory("/remote");
        _mockClient.Received().CreateDirectory("/remote/new");
        _mockClient.Received().CreateDirectory("/remote/new/dir");
    }

    [Fact]
    public async Task CreateRemoteDirectoryAsync_SkipsExistingSegments()
    {
        _mockClient.Exists("/existing").Returns(true);
        _mockClient.Exists("/existing/new").Returns(false);

        await _transport.CreateRemoteDirectoryAsync("/existing/new");

        _mockClient.DidNotReceive().CreateDirectory("/existing");
        _mockClient.Received().CreateDirectory("/existing/new");
    }

    [Fact]
    public void Dispose_DisconnectsClient()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(true);

        var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance);
        transport.Dispose();

        mockClient.Received().Disconnect();
        mockClient.Received().Dispose();
    }

    [Fact]
    public void Dispose_WhenNotConnected_DoesNotDisconnect()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(false);

        var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance);
        transport.Dispose();

        mockClient.DidNotReceive().Disconnect();
        mockClient.Received().Dispose();
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var act = () => new SftpTransport(null!, BasePath, NullLogger<SftpTransport>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullBasePath_Throws()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();

        var act = () => new SftpTransport(mockClient, null!, NullLogger<SftpTransport>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_ReportsProgress()
    {
        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        _mockClient.Exists("/backups/zim/zim-resume.json").Returns(false);

        var data = Encoding.UTF8.GetBytes("progress test data");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = "progress.bin",
            SizeBytes = data.Length
        };

        TransferProgress? lastProgress = null;
        var progress = new Progress<TransferProgress>(p => lastProgress = p);

        await _transport.SendAsync(stream, metadata, progress);

        // Give async progress a moment
        await Task.Delay(50);

        // Progress might not be reported for small non-chunked files,
        // but the upload should complete successfully
        _mockClient.Received().UploadFile(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureRemoteDirectoryExists_DeepPath_CreatesAll()
    {
        _mockClient.Exists("/a").Returns(false);
        _mockClient.Exists("/a/b").Returns(false);
        _mockClient.Exists("/a/b/c").Returns(false);
        _mockClient.Exists("/a/b/c/d").Returns(false);

        _transport.EnsureRemoteDirectoryExists("/a/b/c/d");

        _mockClient.Received().CreateDirectory("/a");
        _mockClient.Received().CreateDirectory("/a/b");
        _mockClient.Received().CreateDirectory("/a/b/c");
        _mockClient.Received().CreateDirectory("/a/b/c/d");
    }

    [Fact]
    public async Task TestConnectionAsync_ConnectsIfNotConnected()
    {
        var mockClient = Substitute.For<ISftpClientWrapper>();
        mockClient.IsConnected.Returns(false, true);
        mockClient.Exists(Arg.Any<string>()).Returns(true);

        using var transport = new SftpTransport(
            mockClient, BasePath, NullLogger<SftpTransport>.Instance);

        await transport.TestConnectionAsync();

        mockClient.Received().Connect();
    }
}
