using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE.Tests.Services;

public class ImageBrowserServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImageBrowserService _service;

    public ImageBrowserServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new ImageBrowserService(NullLogger<ImageBrowserService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task FindImagesAsync_FindsImgFiles()
    {
        // Create a test .img file
        var imgPath = Path.Combine(_tempDir, "test.img");
        await File.WriteAllBytesAsync(imgPath, new byte[1024]);

        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().HaveCount(1);
        results[0].ImagePath.Should().Be(imgPath);
    }

    [Fact]
    public async Task FindImagesAsync_FindsVhdxFiles()
    {
        var vhdxPath = Path.Combine(_tempDir, "test.vhdx");
        await File.WriteAllBytesAsync(vhdxPath, new byte[2048]);

        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().HaveCount(1);
        results[0].ImagePath.Should().Be(vhdxPath);
        results[0].FileSizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task FindImagesAsync_LoadsMetadataWhenAvailable()
    {
        var imgPath = Path.Combine(_tempDir, "test.img");
        await File.WriteAllBytesAsync(imgPath, new byte[512]);

        var metadata = new DiskImageMetadata
        {
            SourceHostname = "TESTPC",
            SourceOsVersion = "Windows 10 Pro",
            Format = DiskImageFormat.Img
        };
        await metadata.SaveAsync(imgPath);

        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().HaveCount(1);
        results[0].Metadata.Should().NotBeNull();
        results[0].Metadata!.SourceHostname.Should().Be("TESTPC");
    }

    [Fact]
    public async Task FindImagesAsync_NullMetadata_WhenNoMetaFile()
    {
        var imgPath = Path.Combine(_tempDir, "nometadata.raw");
        await File.WriteAllBytesAsync(imgPath, new byte[256]);

        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().HaveCount(1);
        results[0].Metadata.Should().BeNull();
    }

    [Fact]
    public async Task FindImagesAsync_EmptyDirectory_ReturnsEmpty()
    {
        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindImagesAsync_ReportsFileSize()
    {
        var imgPath = Path.Combine(_tempDir, "sized.img");
        var data = new byte[4096];
        await File.WriteAllBytesAsync(imgPath, data);

        var results = await _service.FindImagesAsync(_tempDir);

        results.Should().HaveCount(1);
        results[0].FileSizeBytes.Should().Be(4096);
    }
}
