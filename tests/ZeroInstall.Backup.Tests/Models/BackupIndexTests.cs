using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests.Models;

public class BackupIndexTests
{
    [Fact]
    public void GetChangedFiles_DetectsNewFiles()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" },
            new() { RelativePath = "b.txt", SizeBytes = 200, Sha256 = "bbb" }
        };

        var changed = index.GetChangedFiles(current);

        changed.Should().HaveCount(1);
        changed[0].RelativePath.Should().Be("b.txt");
    }

    [Fact]
    public void GetChangedFiles_DetectsModifiedBySize()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 200, Sha256 = "aaa-new" }
        };

        var changed = index.GetChangedFiles(current);

        changed.Should().HaveCount(1);
        changed[0].RelativePath.Should().Be("a.txt");
    }

    [Fact]
    public void GetChangedFiles_DetectsModifiedByHash()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "bbb" }
        };

        var changed = index.GetChangedFiles(current);

        changed.Should().HaveCount(1);
    }

    [Fact]
    public void GetChangedFiles_ReturnsEmpty_WhenNothingChanged()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
        };

        index.GetChangedFiles(current).Should().BeEmpty();
    }

    [Fact]
    public void GetChangedFiles_IsCaseInsensitive()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "Docs/A.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "docs/a.txt", SizeBytes = 100, Sha256 = "AAA" }
        };

        index.GetChangedFiles(current).Should().BeEmpty();
    }

    [Fact]
    public void GetDeletedFiles_DetectsRemovedFiles()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" },
                new() { RelativePath = "b.txt", SizeBytes = 200, Sha256 = "bbb" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
        };

        var deleted = index.GetDeletedFiles(current);

        deleted.Should().HaveCount(1);
        deleted[0].RelativePath.Should().Be("b.txt");
    }

    [Fact]
    public void GetDeletedFiles_ReturnsEmpty_WhenAllPresent()
    {
        var index = new BackupIndex
        {
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
            }
        };

        var current = new List<BackupFileEntry>
        {
            new() { RelativePath = "a.txt", SizeBytes = 100, Sha256 = "aaa" }
        };

        index.GetDeletedFiles(current).Should().BeEmpty();
    }
}
