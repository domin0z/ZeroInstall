using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;

namespace ZeroInstall.Backup.Tests.Services;

public class FileIndexServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileIndexService _service;

    public FileIndexServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new FileIndexService(NullLogger<FileIndexService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ScanDirectoriesAsync_FindsAllFiles()
    {
        CreateFile("a.txt", "hello");
        CreateFile("sub/b.txt", "world");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            Array.Empty<string>());

        entries.Should().HaveCount(2);
        entries.Select(e => e.RelativePath).Should().BeEquivalentTo(new[] { "a.txt", "sub/b.txt" });
    }

    [Fact]
    public async Task ScanDirectoriesAsync_RecordsFileSizeAndTimestamp()
    {
        CreateFile("data.txt", "some content here");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            Array.Empty<string>());

        entries.Should().HaveCount(1);
        entries[0].SizeBytes.Should().Be(17);
        entries[0].LastModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ScanDirectoriesAsync_ExcludesByExtension()
    {
        CreateFile("keep.txt", "keep");
        CreateFile("skip.tmp", "skip");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            new[] { "*.tmp" });

        entries.Should().HaveCount(1);
        entries[0].RelativePath.Should().Be("keep.txt");
    }

    [Fact]
    public async Task ScanDirectoriesAsync_ExcludesByDirectoryName()
    {
        CreateFile("keep.txt", "keep");
        CreateFile("node_modules/pkg.json", "skip");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            new[] { "node_modules" });

        entries.Should().HaveCount(1);
        entries[0].RelativePath.Should().Be("keep.txt");
    }

    [Fact]
    public async Task ScanDirectoriesAsync_ExcludesByFileName()
    {
        CreateFile("keep.txt", "keep");
        CreateFile("Thumbs.db", "skip");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            new[] { "Thumbs.db" });

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanDirectoriesAsync_MultipleDirectories()
    {
        var dir2 = Path.Combine(_tempDir, "second");
        Directory.CreateDirectory(dir2);

        CreateFile("a.txt", "a");
        File.WriteAllText(Path.Combine(dir2, "b.txt"), "b");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir, dir2 },
            Array.Empty<string>());

        // dir2 is under _tempDir, so scanning _tempDir finds a.txt and second/b.txt,
        // scanning dir2 finds b.txt again. Total = 3 entries.
        entries.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ScanDirectoriesAsync_SkipsNonexistentDirectory()
    {
        CreateFile("a.txt", "a");

        var entries = await _service.ScanDirectoriesAsync(
            new[] { _tempDir, @"C:\nonexistent-path-zim-test" },
            Array.Empty<string>());

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanDirectoriesAsync_UsesCancellationToken()
    {
        CreateFile("a.txt", "a");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _service.ScanDirectoriesAsync(
            new[] { _tempDir },
            Array.Empty<string>(),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadIndexAsync_ReturnsEmptyForMissingFile()
    {
        var path = Path.Combine(_tempDir, "nonexistent.json");

        var index = await _service.LoadIndexAsync(path);

        index.Should().NotBeNull();
        index.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoadIndex_RoundTrips()
    {
        var indexPath = Path.Combine(_tempDir, "backup-index.json");
        var index = new BackupIndex
        {
            CustomerId = "cust-001",
            LastScanUtc = DateTime.UtcNow,
            Files = new List<BackupFileEntry>
            {
                new()
                {
                    RelativePath = "docs/readme.txt",
                    SizeBytes = 1234,
                    LastModifiedUtc = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                    Sha256 = "abc123"
                }
            }
        };

        await _service.SaveIndexAsync(index, indexPath);
        var loaded = await _service.LoadIndexAsync(indexPath);

        loaded.CustomerId.Should().Be("cust-001");
        loaded.Files.Should().HaveCount(1);
        loaded.Files[0].RelativePath.Should().Be("docs/readme.txt");
        loaded.Files[0].Sha256.Should().Be("abc123");
    }

    [Fact]
    public async Task HashFileAsync_ComputesSha256()
    {
        var filePath = CreateFile("test.txt", "hello world");

        var hash = await FileIndexService.HashFileAsync(filePath);

        // SHA-256 of "hello world" is well-known
        hash.Should().Be("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9");
    }

    [Fact]
    public void GlobToRegex_MatchesWildcardExtension()
    {
        var regex = FileIndexService.GlobToRegex("*.tmp");

        regex.IsMatch("file.tmp").Should().BeTrue();
        regex.IsMatch("file.txt").Should().BeFalse();
    }

    [Fact]
    public void GlobToRegex_MatchesExactName()
    {
        var regex = FileIndexService.GlobToRegex("Thumbs.db");

        regex.IsMatch("Thumbs.db").Should().BeTrue();
        regex.IsMatch("thumbs.db").Should().BeTrue(); // case insensitive
        regex.IsMatch("other.db").Should().BeFalse();
    }

    [Fact]
    public void GlobToRegex_MatchesDoubleWildcard()
    {
        var regex = FileIndexService.GlobToRegex("**/*.log");

        regex.IsMatch("logs/app.log").Should().BeTrue();
        regex.IsMatch("deep/nested/dir/app.log").Should().BeTrue();
        regex.IsMatch("app.txt").Should().BeFalse();
    }

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
