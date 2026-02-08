using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class ImageSplitterTests : IDisposable
{
    private readonly string _tempDir;

    public ImageSplitterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-split-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Constants

    [Fact]
    public void Fat32MaxFileSize_IsCorrectValue()
    {
        ImageSplitter.Fat32MaxFileSize.Should().Be(4L * 1024 * 1024 * 1024 - 1);
    }

    [Fact]
    public void DefaultChunkSize_IsUnderFat32Limit()
    {
        ImageSplitter.DefaultChunkSize.Should().BeLessThan(ImageSplitter.Fat32MaxFileSize);
    }

    #endregion

    #region NeedsSplitting

    [Fact]
    public void NeedsSplitting_SmallFile_ReturnsFalse()
    {
        ImageSplitter.NeedsSplitting(1024 * 1024).Should().BeFalse();
    }

    [Fact]
    public void NeedsSplitting_ExactlyAtLimit_ReturnsFalse()
    {
        ImageSplitter.NeedsSplitting(ImageSplitter.Fat32MaxFileSize).Should().BeFalse();
    }

    [Fact]
    public void NeedsSplitting_OverLimit_ReturnsTrue()
    {
        ImageSplitter.NeedsSplitting(ImageSplitter.Fat32MaxFileSize + 1).Should().BeTrue();
    }

    [Fact]
    public void NeedsSplitting_CustomLimit_RespectsIt()
    {
        ImageSplitter.NeedsSplitting(500, maxChunkSize: 100).Should().BeTrue();
        ImageSplitter.NeedsSplitting(50, maxChunkSize: 100).Should().BeFalse();
    }

    #endregion

    #region CalculateChunkCount

    [Fact]
    public void CalculateChunkCount_ZeroBytes_ReturnsOne()
    {
        ImageSplitter.CalculateChunkCount(0).Should().Be(1);
    }

    [Fact]
    public void CalculateChunkCount_NegativeBytes_ReturnsOne()
    {
        ImageSplitter.CalculateChunkCount(-100).Should().Be(1);
    }

    [Fact]
    public void CalculateChunkCount_SmallFile_ReturnsOne()
    {
        ImageSplitter.CalculateChunkCount(1024, chunkSize: 4096).Should().Be(1);
    }

    [Fact]
    public void CalculateChunkCount_ExactMultiple()
    {
        ImageSplitter.CalculateChunkCount(200, chunkSize: 100).Should().Be(2);
    }

    [Fact]
    public void CalculateChunkCount_WithRemainder()
    {
        ImageSplitter.CalculateChunkCount(250, chunkSize: 100).Should().Be(3);
    }

    [Fact]
    public void CalculateChunkCount_LargeFile()
    {
        // 10 GB at ~4 GB chunks = 3 chunks
        var tenGB = 10L * 1024 * 1024 * 1024;
        ImageSplitter.CalculateChunkCount(tenGB).Should().Be(3);
    }

    #endregion

    #region SplitAsync

    [Fact]
    public async Task SplitAsync_ThrowsIfSourceNotFound()
    {
        var nonExistent = Path.Combine(_tempDir, "nope.img");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => ImageSplitter.SplitAsync(nonExistent, chunkSize: 100));
    }

    [Fact]
    public async Task SplitAsync_SmallFileProducesOneChunk()
    {
        var sourceFile = Path.Combine(_tempDir, "small.img");
        var data = new byte[50];
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        var chunks = await ImageSplitter.SplitAsync(sourceFile, chunkSize: 100);

        chunks.Should().HaveCount(1);
        File.Exists(chunks[0]).Should().BeTrue();
        (await File.ReadAllBytesAsync(chunks[0])).Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SplitAsync_SplitsIntoCorrectNumberOfChunks()
    {
        var sourceFile = Path.Combine(_tempDir, "medium.img");
        var data = new byte[250];
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        var chunks = await ImageSplitter.SplitAsync(sourceFile, chunkSize: 100);

        chunks.Should().HaveCount(3);
        foreach (var chunk in chunks)
            File.Exists(chunk).Should().BeTrue();
    }

    [Fact]
    public async Task SplitAsync_ChunksContainCorrectData()
    {
        var sourceFile = Path.Combine(_tempDir, "verify.img");
        var data = new byte[250];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 256);
        await File.WriteAllBytesAsync(sourceFile, data);

        var chunks = await ImageSplitter.SplitAsync(sourceFile, chunkSize: 100);

        var chunk0 = await File.ReadAllBytesAsync(chunks[0]);
        var chunk1 = await File.ReadAllBytesAsync(chunks[1]);
        var chunk2 = await File.ReadAllBytesAsync(chunks[2]);

        chunk0.Should().HaveCount(100);
        chunk1.Should().HaveCount(100);
        chunk2.Should().HaveCount(50);

        // Verify data integrity
        chunk0.Should().BeEquivalentTo(data[..100]);
        chunk1.Should().BeEquivalentTo(data[100..200]);
        chunk2.Should().BeEquivalentTo(data[200..250]);
    }

    [Fact]
    public async Task SplitAsync_UsesCorrectChunkNaming()
    {
        var sourceFile = Path.Combine(_tempDir, "naming.img");
        await File.WriteAllBytesAsync(sourceFile, new byte[250]);

        var chunks = await ImageSplitter.SplitAsync(sourceFile, chunkSize: 100);

        chunks[0].Should().EndWith("naming.part0000.img");
        chunks[1].Should().EndWith("naming.part0001.img");
        chunks[2].Should().EndWith("naming.part0002.img");
    }

    [Fact]
    public async Task SplitAsync_ReportsProgress()
    {
        var sourceFile = Path.Combine(_tempDir, "progress.img");
        await File.WriteAllBytesAsync(sourceFile, new byte[200]);

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        await ImageSplitter.SplitAsync(sourceFile, chunkSize: 100, progress: progress);

        // Give Progress<T> time to invoke callbacks (it posts to sync context)
        await Task.Delay(100);

        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.TotalItems == 2);
    }

    [Fact]
    public async Task SplitAsync_SupportsCancellation()
    {
        var sourceFile = Path.Combine(_tempDir, "cancel.img");
        await File.WriteAllBytesAsync(sourceFile, new byte[500]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ImageSplitter.SplitAsync(sourceFile, chunkSize: 100, ct: cts.Token));
    }

    #endregion

    #region ReassembleAsync

    [Fact]
    public async Task ReassembleAsync_ThrowsIfChunkMissing()
    {
        var baseImagePath = Path.Combine(_tempDir, "missing.img");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => ImageSplitter.ReassembleAsync(
                Path.Combine(_tempDir, "output.img"), 2, baseImagePath));
    }

    [Fact]
    public async Task ReassembleAsync_ReassemblesCorrectly()
    {
        // Create chunks manually
        var baseImagePath = Path.Combine(_tempDir, "reassemble.img");
        var data = new byte[250];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 256);

        // Write chunk files
        await File.WriteAllBytesAsync(DiskImageMetadata.GetChunkPath(baseImagePath, 0), data[..100]);
        await File.WriteAllBytesAsync(DiskImageMetadata.GetChunkPath(baseImagePath, 1), data[100..200]);
        await File.WriteAllBytesAsync(DiskImageMetadata.GetChunkPath(baseImagePath, 2), data[200..250]);

        var outputPath = Path.Combine(_tempDir, "output.img");
        await ImageSplitter.ReassembleAsync(outputPath, 3, baseImagePath);

        var result = await File.ReadAllBytesAsync(outputPath);
        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SplitAndReassemble_RoundTrip()
    {
        // Create original file
        var originalPath = Path.Combine(_tempDir, "original.img");
        var data = new byte[1024];
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(originalPath, data);

        // Split
        var chunks = await ImageSplitter.SplitAsync(originalPath, chunkSize: 300);

        // Reassemble
        var reassembledPath = Path.Combine(_tempDir, "reassembled.img");
        await ImageSplitter.ReassembleAsync(reassembledPath, chunks.Count, originalPath);

        // Verify data integrity
        var reassembled = await File.ReadAllBytesAsync(reassembledPath);
        reassembled.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ReassembleAsync_ReportsProgress()
    {
        var baseImagePath = Path.Combine(_tempDir, "prog.img");
        await File.WriteAllBytesAsync(DiskImageMetadata.GetChunkPath(baseImagePath, 0), new byte[100]);
        await File.WriteAllBytesAsync(DiskImageMetadata.GetChunkPath(baseImagePath, 1), new byte[50]);

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        var outputPath = Path.Combine(_tempDir, "prog-output.img");
        await ImageSplitter.ReassembleAsync(outputPath, 2, baseImagePath, progress);

        await Task.Delay(100);

        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.TotalItems == 2);
    }

    #endregion

    #region IsFat32

    [Fact]
    public void IsFat32_NullPath_ReturnsFalse()
    {
        // Invalid path shouldn't crash
        ImageSplitter.IsFat32("").Should().BeFalse();
    }

    [Fact]
    public void IsFat32_SystemDrive_TypicallyNotFat32()
    {
        // The system drive (C:) is typically NTFS, not FAT32
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        ImageSplitter.IsFat32(systemDrive).Should().BeFalse();
    }

    #endregion
}
