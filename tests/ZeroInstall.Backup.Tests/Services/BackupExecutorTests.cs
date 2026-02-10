using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Tests.Services;

public class BackupExecutorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dataDir;
    private readonly ISftpClientWrapper _mockClient;
    private readonly ISftpClientFactory _mockFactory;
    private readonly IRetentionService _mockRetention;
    private readonly FileIndexService _realIndexService;
    private readonly BackupExecutor _executor;

    public BackupExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-backup-test-{Guid.NewGuid():N}");
        _dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_dataDir);

        _mockClient = Substitute.For<ISftpClientWrapper>();
        _mockFactory = Substitute.For<ISftpClientFactory>();
        _mockFactory.Create(Arg.Any<SftpTransportConfiguration>()).Returns(_mockClient);

        _mockRetention = Substitute.For<IRetentionService>();
        _realIndexService = new FileIndexService(NullLogger<FileIndexService>.Instance);

        _executor = new BackupExecutor(
            _realIndexService,
            _mockRetention,
            _mockFactory,
            NullLogger<BackupExecutor>.Instance)
        {
            LocalDataPath = _dataDir
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateBackupDir()
    {
        var dir = Path.Combine(_tempDir, "source");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private BackupConfiguration CreateTestConfig(string sourceDir)
    {
        return new BackupConfiguration
        {
            CustomerId = "test-cust",
            BackupPaths = { sourceDir },
            ExcludePatterns = { "*.tmp" },
            NasConnection = new SftpTransportConfiguration
            {
                Host = "nas.local",
                Username = "user",
                Password = "pass",
                RemoteBasePath = "/backups"
            },
            Retention = new RetentionPolicy { KeepLastFileBackups = 5 }
        };
    }

    [Fact]
    public async Task RunFileBackupAsync_ReturnsSkipped_WhenNoChanges()
    {
        var sourceDir = CreateBackupDir();
        // Empty directory — no files to back up
        var config = CreateTestConfig(sourceDir);
        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        var result = await _executor.RunFileBackupAsync(config);

        result.ResultType.Should().Be(BackupRunResultType.Skipped);
        result.BackupType.Should().Be("file");
        result.FilesScanned.Should().Be(0);
    }

    [Fact]
    public async Task RunFileBackupAsync_UploadsNewFiles()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "hello world");

        var config = CreateTestConfig(sourceDir);
        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        var result = await _executor.RunFileBackupAsync(config);

        result.ResultType.Should().Be(BackupRunResultType.Success);
        result.FilesScanned.Should().Be(1);
        result.FilesUploaded.Should().Be(1);
        result.FilesFailed.Should().Be(0);
        result.BytesTransferred.Should().BeGreaterThan(0);

        // Verify file was uploaded
        _mockClient.Received().UploadFile(Arg.Any<Stream>(), Arg.Is<string>(s => s.Contains("test.txt")));
    }

    [Fact]
    public async Task RunFileBackupAsync_ExcludesMatchingPatterns()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(sourceDir, "skip.tmp"), "skip");

        var config = CreateTestConfig(sourceDir);
        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        var result = await _executor.RunFileBackupAsync(config);

        result.FilesScanned.Should().Be(1);
        result.FilesUploaded.Should().Be(1);
    }

    [Fact]
    public async Task RunFileBackupAsync_ReturnsQuotaExceeded_WhenOverQuota()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "data");

        var config = CreateTestConfig(sourceDir);
        config.QuotaBytes = 100; // very low quota

        _mockClient.Exists(Arg.Any<string>()).Returns(true);
        _mockRetention.CalculateNasUsageAsync(Arg.Any<ISftpClientWrapper>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(200L); // over quota
        _mockRetention.EnforceRetentionAsync(Arg.Any<ISftpClientWrapper>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _executor.RunFileBackupAsync(config);

        result.ResultType.Should().Be(BackupRunResultType.QuotaExceeded);
    }

    [Fact]
    public async Task RunFileBackupAsync_EnforcesRetention()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "data");

        var config = CreateTestConfig(sourceDir);
        config.Retention.KeepLastFileBackups = 3;

        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        await _executor.RunFileBackupAsync(config);

        await _mockRetention.Received().EnforceRetentionAsync(
            _mockClient,
            Arg.Is<string>(s => s.Contains("file-backups")),
            3,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunFileBackupAsync_WritesManifest()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "data");

        var config = CreateTestConfig(sourceDir);
        _mockClient.Exists(Arg.Any<string>()).Returns(false);

        await _executor.RunFileBackupAsync(config);

        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            Arg.Is<string>(s => s.Contains("zim-manifest.json")));
    }

    [Fact]
    public async Task RunFileBackupAsync_HandlesUploadFailureGracefully()
    {
        var sourceDir = CreateBackupDir();
        File.WriteAllText(Path.Combine(sourceDir, "test.txt"), "data");

        var config = CreateTestConfig(sourceDir);
        config.CompressBeforeUpload = false;

        _mockClient.Exists(Arg.Any<string>()).Returns(false);
        _mockClient.When(c => c.UploadFile(Arg.Any<Stream>(), Arg.Is<string>(s => s.Contains("test.txt"))))
            .Do(_ => throw new IOException("Network error"));

        var result = await _executor.RunFileBackupAsync(config);

        result.ResultType.Should().Be(BackupRunResultType.PartialSuccess);
        result.FilesFailed.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("Network error"));
    }

    [Fact]
    public async Task RunFullImageBackupAsync_ReturnsSkipped()
    {
        var sourceDir = CreateBackupDir();
        var config = CreateTestConfig(sourceDir);

        var result = await _executor.RunFullImageBackupAsync(config);

        result.ResultType.Should().Be(BackupRunResultType.Skipped);
        result.BackupType.Should().Be("full-image");
    }
}
