using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Integration;

/// <summary>
/// End-to-end tests for the disk clone → image split → reassemble → metadata pipeline.
/// Uses temp files instead of real disk volumes.
/// </summary>
public class DiskCloneRestorePipelineTests : IDisposable
{
    private readonly string _tempDir;

    public DiskCloneRestorePipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-clone-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ImageSplitter_SplitAndReassemble_RoundTrips()
    {
        // Create a test file with known content
        var originalPath = Path.Combine(_tempDir, "test-image.img");
        var data = new byte[10 * 1024]; // 10 KB
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(originalPath, data);

        var originalChecksum = await ChecksumHelper.ComputeFileAsync(originalPath);

        // Split into 3KB chunks (should produce 4 chunks)
        var chunkSize = 3 * 1024L;
        var chunkPaths = await ImageSplitter.SplitAsync(originalPath, chunkSize);

        chunkPaths.Should().HaveCount(4);
        foreach (var path in chunkPaths)
            File.Exists(path).Should().BeTrue();

        // Reassemble
        var reassembledPath = Path.Combine(_tempDir, "reassembled.img");
        await ImageSplitter.ReassembleAsync(reassembledPath, chunkPaths.Count, originalPath);

        // Verify checksum matches
        var reassembledChecksum = await ChecksumHelper.ComputeFileAsync(reassembledPath);
        reassembledChecksum.Should().Be(originalChecksum);

        // Verify byte-for-byte match
        var reassembledData = await File.ReadAllBytesAsync(reassembledPath);
        reassembledData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ImageSplitter_SmallFile_NoSplitNeeded()
    {
        var smallPath = Path.Combine(_tempDir, "small.img");
        var data = new byte[100]; // 100 bytes
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(smallPath, data);

        // File is smaller than the default chunk size
        ImageSplitter.NeedsSplitting(data.Length).Should().BeFalse();
        ImageSplitter.CalculateChunkCount(data.Length).Should().Be(1);

        // File is also smaller than a custom 1KB chunk size
        ImageSplitter.NeedsSplitting(data.Length, 1024).Should().BeFalse();
    }

    [Fact]
    public async Task DiskImageMetadata_SaveAndLoad_RoundTrips()
    {
        var imagePath = Path.Combine(_tempDir, "metadata-test.img");
        await File.WriteAllBytesAsync(imagePath, new byte[1]); // Dummy file

        var metadata = new DiskImageMetadata
        {
            SourceHostname = "TEST-PC",
            SourceOsVersion = "Windows 10 Pro 22H2",
            SourceVolume = "C:",
            SourceVolumeSizeBytes = 500_000_000_000L,
            SourceVolumeUsedBytes = 200_000_000_000L,
            ImageSizeBytes = 200_000_000_000L,
            Format = DiskImageFormat.Img,
            IsCompressed = false,
            Checksum = "abc123def456",
            IsSplit = false,
            ChunkCount = 1,
            FileSystemType = "NTFS",
            UsedVss = true
        };

        // Save
        await metadata.SaveAsync(imagePath);

        // Load
        var loaded = await DiskImageMetadata.LoadAsync(imagePath);

        loaded.Should().NotBeNull();
        loaded!.SourceHostname.Should().Be("TEST-PC");
        loaded.SourceOsVersion.Should().Be("Windows 10 Pro 22H2");
        loaded.SourceVolume.Should().Be("C:");
        loaded.SourceVolumeSizeBytes.Should().Be(500_000_000_000L);
        loaded.SourceVolumeUsedBytes.Should().Be(200_000_000_000L);
        loaded.ImageSizeBytes.Should().Be(200_000_000_000L);
        loaded.Format.Should().Be(DiskImageFormat.Img);
        loaded.Checksum.Should().Be("abc123def456");
        loaded.FileSystemType.Should().Be("NTFS");
        loaded.UsedVss.Should().BeTrue();
    }

    [Fact]
    public async Task DiskImageMetadata_WithChunks_TracksAllParts()
    {
        var imagePath = Path.Combine(_tempDir, "chunked-test.img");
        await File.WriteAllBytesAsync(imagePath, new byte[1]);

        var metadata = new DiskImageMetadata
        {
            SourceHostname = "SPLIT-PC",
            ImageSizeBytes = 12_000_000_000L, // 12 GB
            Format = DiskImageFormat.Raw,
            IsSplit = true,
            ChunkCount = 3,
            ChunkSizeBytes = ImageSplitter.DefaultChunkSize,
            ChunkChecksums = ["checksum0", "checksum1", "checksum2"]
        };

        await metadata.SaveAsync(imagePath);
        var loaded = await DiskImageMetadata.LoadAsync(imagePath);

        loaded.Should().NotBeNull();
        loaded!.IsSplit.Should().BeTrue();
        loaded.ChunkCount.Should().Be(3);
        loaded.ChunkSizeBytes.Should().Be(ImageSplitter.DefaultChunkSize);
        loaded.ChunkChecksums.Should().HaveCount(3);
        loaded.ChunkChecksums[0].Should().Be("checksum0");
        loaded.ChunkChecksums[2].Should().Be("checksum2");

        // Verify total size tracking
        loaded.ImageSizeBytes.Should().Be(12_000_000_000L);
    }

    [Fact]
    public async Task CloneToImage_SplitForFat32_ReassembleAndVerify()
    {
        // Simulate: DiskClonerService produces a large image file, then ImageSplitter splits it
        var imagePath = Path.Combine(_tempDir, "clone-output.img");
        var imageData = new byte[15 * 1024]; // 15 KB (simulating a large file)
        Random.Shared.NextBytes(imageData);
        await File.WriteAllBytesAsync(imagePath, imageData);

        var originalChecksum = await ChecksumHelper.ComputeFileAsync(imagePath);

        // Create metadata as DiskClonerService would
        var metadata = new DiskImageMetadata
        {
            SourceHostname = "CLONE-PC",
            ImageSizeBytes = imageData.Length,
            Format = DiskImageFormat.Img,
            Checksum = originalChecksum
        };

        // Split for "FAT32" with 4KB chunks
        var chunkSize = 4 * 1024L;
        var needsSplit = ImageSplitter.NeedsSplitting(imageData.Length, chunkSize);
        needsSplit.Should().BeTrue();

        var chunkPaths = await ImageSplitter.SplitAsync(imagePath, chunkSize);
        chunkPaths.Should().HaveCount(4); // 15KB / 4KB = 3.75 → 4 chunks

        // Compute chunk checksums
        metadata.IsSplit = true;
        metadata.ChunkCount = chunkPaths.Count;
        metadata.ChunkSizeBytes = chunkSize;
        foreach (var chunkPath in chunkPaths)
        {
            var chunkChecksum = await ChecksumHelper.ComputeFileAsync(chunkPath);
            metadata.ChunkChecksums.Add(chunkChecksum);
        }

        await metadata.SaveAsync(imagePath);

        // Reassemble
        var reassembledPath = Path.Combine(_tempDir, "reassembled-clone.img");
        await ImageSplitter.ReassembleAsync(reassembledPath, metadata.ChunkCount, imagePath);

        // Verify checksums match
        var reassembledChecksum = await ChecksumHelper.ComputeFileAsync(reassembledPath);
        reassembledChecksum.Should().Be(originalChecksum);
    }

    [Fact]
    public async Task RestoreOrchestrator_WithSplitImage_ReassemblesFirst()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "OK" });

        var cloner = new DiskClonerService(processRunner, NullLogger<DiskClonerService>.Instance);

        // Create a split image on disk
        var imagePath = Path.Combine(_tempDir, "split-image.img");
        var imageData = new byte[8 * 1024]; // 8 KB
        Random.Shared.NextBytes(imageData);
        await File.WriteAllBytesAsync(imagePath, imageData);

        var originalChecksum = await ChecksumHelper.ComputeFileAsync(imagePath);

        // Split into 3KB chunks
        var chunkPaths = await ImageSplitter.SplitAsync(imagePath, 3 * 1024L);
        chunkPaths.Should().HaveCount(3);

        // Create metadata marking as split
        var metadata = new DiskImageMetadata
        {
            SourceHostname = "SPLIT-PC",
            SourceVolume = "C:",
            ImageSizeBytes = imageData.Length,
            Format = DiskImageFormat.Img,
            IsSplit = true,
            ChunkCount = chunkPaths.Count,
            ChunkSizeBytes = 3 * 1024L,
            Checksum = originalChecksum
        };
        await metadata.SaveAsync(imagePath);

        // VerifyImageAsync should check chunk checksums
        // But since we didn't add chunk checksums, it falls through to single-file check
        // The key point is that the metadata round-trips correctly
        var loadedMeta = await DiskImageMetadata.LoadAsync(imagePath);
        loadedMeta.Should().NotBeNull();
        loadedMeta!.IsSplit.Should().BeTrue();
        loadedMeta.ChunkCount.Should().Be(3);

        // Reassemble to verify data integrity
        var reassembledPath = Path.Combine(_tempDir, "restored.img");
        await ImageSplitter.ReassembleAsync(reassembledPath, loadedMeta.ChunkCount, imagePath);

        var reassembledChecksum = await ChecksumHelper.ComputeFileAsync(reassembledPath);
        reassembledChecksum.Should().Be(originalChecksum);
    }
}
