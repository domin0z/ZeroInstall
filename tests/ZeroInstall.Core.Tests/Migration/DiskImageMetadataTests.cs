using System.Text.Json;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;

namespace ZeroInstall.Core.Tests.Migration;

public class DiskImageMetadataTests : IDisposable
{
    private readonly string _tempDir;

    public DiskImageMetadataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-meta-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Properties

    [Fact]
    public void NewMetadata_HasDefaultValues()
    {
        var meta = new DiskImageMetadata();

        meta.ImageId.Should().NotBeNullOrEmpty();
        meta.CapturedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        meta.SourceHostname.Should().BeEmpty();
        meta.SourceOsVersion.Should().BeEmpty();
        meta.SourceVolume.Should().BeEmpty();
        meta.SourceVolumeSizeBytes.Should().Be(0);
        meta.SourceVolumeUsedBytes.Should().Be(0);
        meta.ImageSizeBytes.Should().Be(0);
        meta.IsCompressed.Should().BeFalse();
        meta.Checksum.Should().BeNull();
        meta.IsSplit.Should().BeFalse();
        meta.ChunkCount.Should().Be(1);
        meta.ChunkSizeBytes.Should().Be(0);
        meta.ChunkChecksums.Should().BeEmpty();
        meta.UsedVss.Should().BeFalse();
        meta.FileSystemType.Should().BeEmpty();
    }

    [Fact]
    public void ImageId_IsUniquePerInstance()
    {
        var meta1 = new DiskImageMetadata();
        var meta2 = new DiskImageMetadata();

        meta1.ImageId.Should().NotBe(meta2.ImageId);
    }

    #endregion

    #region GetExtension

    [Theory]
    [InlineData(DiskImageFormat.Img, ".img")]
    [InlineData(DiskImageFormat.Raw, ".raw")]
    [InlineData(DiskImageFormat.Vhdx, ".vhdx")]
    public void GetExtension_ReturnsCorrectExtension(DiskImageFormat format, string expected)
    {
        DiskImageMetadata.GetExtension(format).Should().Be(expected);
    }

    #endregion

    #region GetMetadataPath

    [Fact]
    public void GetMetadataPath_ChangesExtension()
    {
        var result = DiskImageMetadata.GetMetadataPath(@"C:\images\backup.img");
        result.Should().Be(@"C:\images\backup.zim-meta.json");
    }

    [Fact]
    public void GetMetadataPath_WorksWithVhdx()
    {
        var result = DiskImageMetadata.GetMetadataPath(@"D:\data\clone.vhdx");
        result.Should().Be(@"D:\data\clone.zim-meta.json");
    }

    #endregion

    #region GetChunkPath

    [Fact]
    public void GetChunkPath_FormatsWithPaddedIndex()
    {
        var result = DiskImageMetadata.GetChunkPath(@"C:\images\backup.img", 0);
        result.Should().Be(@"C:\images\backup.part0000.img");
    }

    [Fact]
    public void GetChunkPath_HighIndex()
    {
        var result = DiskImageMetadata.GetChunkPath(@"C:\images\backup.img", 42);
        result.Should().Be(@"C:\images\backup.part0042.img");
    }

    [Fact]
    public void GetChunkPath_PreservesOriginalExtension()
    {
        var result = DiskImageMetadata.GetChunkPath(@"D:\data\disk.raw", 1);
        result.Should().Be(@"D:\data\disk.part0001.raw");
    }

    #endregion

    #region SaveAsync / LoadAsync

    [Fact]
    public async Task SaveAsync_CreatesMetadataFile()
    {
        var imagePath = Path.Combine(_tempDir, "test.img");
        var meta = new DiskImageMetadata
        {
            SourceHostname = "TESTPC",
            SourceVolume = "C:",
            Format = DiskImageFormat.Img,
            ImageSizeBytes = 1024 * 1024
        };

        await meta.SaveAsync(imagePath);

        var metaPath = DiskImageMetadata.GetMetadataPath(imagePath);
        File.Exists(metaPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        var imagePath = Path.Combine(_tempDir, "test.img");
        var meta = new DiskImageMetadata
        {
            SourceHostname = "TESTPC",
            Format = DiskImageFormat.Raw
        };

        await meta.SaveAsync(imagePath);

        var metaPath = DiskImageMetadata.GetMetadataPath(imagePath);
        var json = await File.ReadAllTextAsync(metaPath);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("SourceHostname").GetString().Should().Be("TESTPC");
        parsed.RootElement.GetProperty("Format").GetString().Should().Be("Raw");
    }

    [Fact]
    public async Task LoadAsync_RoundTripsAllProperties()
    {
        var imagePath = Path.Combine(_tempDir, "roundtrip.img");
        var original = new DiskImageMetadata
        {
            SourceHostname = "SRCPC",
            SourceOsVersion = "Windows 10 Pro 22H2",
            SourceVolume = "C:",
            SourceVolumeSizeBytes = 500_000_000_000,
            SourceVolumeUsedBytes = 250_000_000_000,
            ImageSizeBytes = 250_000_000_000,
            Format = DiskImageFormat.Img,
            IsCompressed = true,
            Checksum = "abc123",
            IsSplit = true,
            ChunkCount = 3,
            ChunkSizeBytes = ImageSplitter.DefaultChunkSize,
            ChunkChecksums = ["aaa", "bbb", "ccc"],
            UsedVss = true,
            FileSystemType = "NTFS"
        };

        await original.SaveAsync(imagePath);
        var loaded = await DiskImageMetadata.LoadAsync(imagePath);

        loaded.Should().NotBeNull();
        loaded!.SourceHostname.Should().Be("SRCPC");
        loaded.SourceOsVersion.Should().Be("Windows 10 Pro 22H2");
        loaded.SourceVolume.Should().Be("C:");
        loaded.SourceVolumeSizeBytes.Should().Be(500_000_000_000);
        loaded.SourceVolumeUsedBytes.Should().Be(250_000_000_000);
        loaded.ImageSizeBytes.Should().Be(250_000_000_000);
        loaded.Format.Should().Be(DiskImageFormat.Img);
        loaded.IsCompressed.Should().BeTrue();
        loaded.Checksum.Should().Be("abc123");
        loaded.IsSplit.Should().BeTrue();
        loaded.ChunkCount.Should().Be(3);
        loaded.ChunkSizeBytes.Should().Be(ImageSplitter.DefaultChunkSize);
        loaded.ChunkChecksums.Should().BeEquivalentTo(new[] { "aaa", "bbb", "ccc" });
        loaded.UsedVss.Should().BeTrue();
        loaded.FileSystemType.Should().Be("NTFS");
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenNoMetadataFile()
    {
        var result = await DiskImageMetadata.LoadAsync(Path.Combine(_tempDir, "nonexistent.img"));
        result.Should().BeNull();
    }

    #endregion
}
