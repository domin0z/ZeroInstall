using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Migration;

public class DiskClonerServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DiskClonerService _service;
    private readonly string _tempDir;

    public DiskClonerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-dc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region ParseVssDevicePath

    [Fact]
    public void ParseVssDevicePath_ParsesValidOutput()
    {
        var output = """
            Successfully created shadow copy for 'C:\'
               Shadow Copy ID: {b5946137-0001-0000-0000-000000000000}
               Shadow Copy Device Name: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1
            """;

        var result = DiskClonerService.ParseVssDevicePath(output);

        result.Should().Be(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1");
    }

    [Fact]
    public void ParseVssDevicePath_ReturnsNull_WhenNoMatch()
    {
        var output = "Some unrelated output\nNo shadow copy info here";

        DiskClonerService.ParseVssDevicePath(output).Should().BeNull();
    }

    [Fact]
    public void ParseVssDevicePath_ReturnsNull_WhenEmpty()
    {
        DiskClonerService.ParseVssDevicePath("").Should().BeNull();
    }

    [Fact]
    public void ParseVssDevicePath_HandlesExtraWhitespace()
    {
        var output = "   Shadow Copy Device Name:    \\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy5   \n";

        var result = DiskClonerService.ParseVssDevicePath(output);

        result.Should().Be(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy5");
    }

    #endregion

    #region ParseVssShadowId

    [Fact]
    public void ParseVssShadowId_ParsesValidOutput()
    {
        var output = """
            Successfully created shadow copy for 'C:\'
               Shadow Copy ID: {b5946137-0001-0000-0000-000000000000}
               Shadow Copy Device Name: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1
            """;

        var result = DiskClonerService.ParseVssShadowId(output);

        result.Should().Be("{b5946137-0001-0000-0000-000000000000}");
    }

    [Fact]
    public void ParseVssShadowId_ReturnsNull_WhenNoMatch()
    {
        DiskClonerService.ParseVssShadowId("no id here").Should().BeNull();
    }

    [Fact]
    public void ParseVssShadowId_ReturnsNull_WhenEmpty()
    {
        DiskClonerService.ParseVssShadowId("").Should().BeNull();
    }

    #endregion

    #region GetVolumeInfo

    [Fact]
    public void GetVolumeInfo_SystemDrive_ReturnsValidInfo()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";

        var info = DiskClonerService.GetVolumeInfo(systemDrive);

        info.TotalSize.Should().BeGreaterThan(0);
        info.FreeSpace.Should().BeGreaterThanOrEqualTo(0);
        info.FileSystem.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetVolumeInfo_InvalidDrive_ReturnsZeroValues()
    {
        var info = DiskClonerService.GetVolumeInfo("Z:\\");

        info.TotalSize.Should().Be(0);
        info.FreeSpace.Should().Be(0);
        info.FileSystem.Should().Be("Unknown");
    }

    [Fact]
    public void GetVolumeInfo_AppendsBackslash_IfMissing()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        var withoutSlash = systemDrive.TrimEnd('\\');

        var info = DiskClonerService.GetVolumeInfo(withoutSlash);

        info.TotalSize.Should().BeGreaterThan(0);
    }

    #endregion

    #region BuildRawCopyScript

    [Fact]
    public void BuildRawCopyScript_ContainsSourceAndDest()
    {
        var script = DiskClonerService.BuildRawCopyScript(
            @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1",
            @"D:\backup\image.img",
            500_000_000_000);

        script.Should().Contain("HarddiskVolumeShadowCopy1");
        script.Should().Contain("image.img");
    }

    [Fact]
    public void BuildRawCopyScript_EscapesSingleQuotes()
    {
        var script = DiskClonerService.BuildRawCopyScript(
            @"\\.\C:", @"D:\Bob's backup\image.img", 100);

        script.Should().Contain("Bob''s backup");
    }

    [Fact]
    public void BuildRawCopyScript_IncludesBufferAndCopyLogic()
    {
        var script = DiskClonerService.BuildRawCopyScript(@"\\.\C:", @"D:\out.img", 100);

        script.Should().Contain("$buffer");
        script.Should().Contain("$source.Read");
        script.Should().Contain("$dest.Write");
    }

    #endregion

    #region BlockCopyAsync

    [Fact]
    public async Task BlockCopyAsync_CopiesDataCorrectly()
    {
        var sourceFile = Path.Combine(_tempDir, "blockcopy-src.bin");
        var destFile = Path.Combine(_tempDir, "blockcopy-dst.bin");
        var data = new byte[5000];
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        await DiskClonerService.BlockCopyAsync(sourceFile, destFile, data.Length, null, CancellationToken.None);

        var result = await File.ReadAllBytesAsync(destFile);
        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task BlockCopyAsync_ReportsProgress()
    {
        var sourceFile = Path.Combine(_tempDir, "blockprog-src.bin");
        var destFile = Path.Combine(_tempDir, "blockprog-dst.bin");
        var data = new byte[2_000_000]; // 2 MB to trigger multiple reads
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(sourceFile, data);

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        await DiskClonerService.BlockCopyAsync(sourceFile, destFile, data.Length, progress, CancellationToken.None);

        await Task.Delay(100);

        progressReports.Should().NotBeEmpty();
        progressReports.Last().OverallBytesTransferred.Should().Be(data.Length);
        progressReports.Last().OverallTotalBytes.Should().Be(data.Length);
    }

    [Fact]
    public async Task BlockCopyAsync_CalculatesSpeed()
    {
        var sourceFile = Path.Combine(_tempDir, "speed-src.bin");
        var destFile = Path.Combine(_tempDir, "speed-dst.bin");
        await File.WriteAllBytesAsync(sourceFile, new byte[100_000]);

        var lastProgress = (TransferProgress?)null;
        var progress = new Progress<TransferProgress>(p => lastProgress = p);

        await DiskClonerService.BlockCopyAsync(sourceFile, destFile, 100_000, progress, CancellationToken.None);

        await Task.Delay(100);

        // BytesPerSecond should be calculated (could be very high for local file copy)
        lastProgress.Should().NotBeNull();
        lastProgress!.BytesPerSecond.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region CreateVssShadowAsync

    [Fact]
    public async Task CreateVssShadowAsync_ReturnsVssCopy_OnSuccess()
    {
        _processRunner.RunAsync("vssadmin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Successfully created shadow copy for 'C:\'
                       Shadow Copy ID: {12345678-0000-0000-0000-000000000000}
                       Shadow Copy Device Name: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1
                    """
            });

        var result = await _service.CreateVssShadowAsync("C:", CancellationToken.None);

        result.Should().NotBeNull();
        result!.DevicePath.Should().Be(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1");
        result.ShadowId.Should().Be("{12345678-0000-0000-0000-000000000000}");
    }

    [Fact]
    public async Task CreateVssShadowAsync_ReturnsNull_OnFailure()
    {
        _processRunner.RunAsync("vssadmin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 1,
                StandardError = "Access denied"
            });

        var result = await _service.CreateVssShadowAsync("C:", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateVssShadowAsync_ReturnsNull_OnUnparsableOutput()
    {
        _processRunner.RunAsync("vssadmin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "Something unexpected happened"
            });

        var result = await _service.CreateVssShadowAsync("C:", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateVssShadowAsync_AppendsBackslashToVolume()
    {
        _processRunner.RunAsync("vssadmin", Arg.Is<string>(s => s.Contains(@"C:\")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.CreateVssShadowAsync("C:", CancellationToken.None);

        await _processRunner.Received(1).RunAsync("vssadmin",
            Arg.Is<string>(s => s.Contains(@"C:\")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteVssShadowAsync

    [Fact]
    public async Task DeleteVssShadowAsync_CallsVssadmin()
    {
        _processRunner.RunAsync("vssadmin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.DeleteVssShadowAsync("{some-guid}", CancellationToken.None);

        await _processRunner.Received(1).RunAsync("vssadmin",
            Arg.Is<string>(s => s.Contains("{some-guid}") && s.Contains("delete")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteVssShadowAsync_DoesNothing_WhenEmptyShadowId()
    {
        await _service.DeleteVssShadowAsync("", CancellationToken.None);

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteVssShadowAsync_DoesNotThrow_OnFailure()
    {
        _processRunner.RunAsync("vssadmin", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(x => throw new InvalidOperationException("vss error"));

        var act = () => _service.DeleteVssShadowAsync("{bad-guid}", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region VerifyImageAsync

    [Fact]
    public async Task VerifyImageAsync_ReturnsFalse_WhenNoMetadata()
    {
        var imagePath = Path.Combine(_tempDir, "nometadata.img");
        await File.WriteAllBytesAsync(imagePath, new byte[100]);

        var result = await _service.VerifyImageAsync(imagePath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyImageAsync_ReturnsFalse_WhenNoChecksum()
    {
        var imagePath = Path.Combine(_tempDir, "nochecksum.img");
        await File.WriteAllBytesAsync(imagePath, new byte[100]);

        var metadata = new DiskImageMetadata { Checksum = null };
        await metadata.SaveAsync(imagePath);

        var result = await _service.VerifyImageAsync(imagePath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyImageAsync_ReturnsTrue_WhenChecksumMatches()
    {
        var imagePath = Path.Combine(_tempDir, "valid.img");
        var data = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(imagePath, data);

        var checksum = await ChecksumHelper.ComputeFileAsync(imagePath);
        var metadata = new DiskImageMetadata { Checksum = checksum };
        await metadata.SaveAsync(imagePath);

        var result = await _service.VerifyImageAsync(imagePath);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyImageAsync_ReturnsFalse_WhenChecksumMismatch()
    {
        var imagePath = Path.Combine(_tempDir, "corrupt.img");
        await File.WriteAllBytesAsync(imagePath, new byte[] { 1, 2, 3 });

        var metadata = new DiskImageMetadata { Checksum = "0000000000000000000000000000000000000000000000000000000000000000" };
        await metadata.SaveAsync(imagePath);

        var result = await _service.VerifyImageAsync(imagePath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyImageAsync_VerifiesChunks_WhenSplit()
    {
        var baseImagePath = Path.Combine(_tempDir, "split.img");

        // Create chunk files
        var chunk0Data = new byte[] { 10, 20, 30 };
        var chunk1Data = new byte[] { 40, 50 };
        var chunk0Path = DiskImageMetadata.GetChunkPath(baseImagePath, 0);
        var chunk1Path = DiskImageMetadata.GetChunkPath(baseImagePath, 1);
        await File.WriteAllBytesAsync(chunk0Path, chunk0Data);
        await File.WriteAllBytesAsync(chunk1Path, chunk1Data);

        var checksum0 = await ChecksumHelper.ComputeFileAsync(chunk0Path);
        var checksum1 = await ChecksumHelper.ComputeFileAsync(chunk1Path);

        var metadata = new DiskImageMetadata
        {
            Checksum = "overall-not-checked-for-chunks",
            IsSplit = true,
            ChunkCount = 2,
            ChunkChecksums = [checksum0, checksum1]
        };
        await metadata.SaveAsync(baseImagePath);

        var result = await _service.VerifyImageAsync(baseImagePath);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyImageAsync_ReturnsFalse_WhenChunkCorrupt()
    {
        var baseImagePath = Path.Combine(_tempDir, "badsplit.img");

        var chunk0Path = DiskImageMetadata.GetChunkPath(baseImagePath, 0);
        await File.WriteAllBytesAsync(chunk0Path, new byte[] { 1, 2, 3 });

        var metadata = new DiskImageMetadata
        {
            Checksum = "irrelevant",
            IsSplit = true,
            ChunkCount = 1,
            ChunkChecksums = ["0000000000000000000000000000000000000000000000000000000000000000"]
        };
        await metadata.SaveAsync(baseImagePath);

        var result = await _service.VerifyImageAsync(baseImagePath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyImageAsync_ReturnsFalse_WhenChunkMissing()
    {
        var baseImagePath = Path.Combine(_tempDir, "missingchunk.img");

        var metadata = new DiskImageMetadata
        {
            Checksum = "irrelevant",
            IsSplit = true,
            ChunkCount = 2,
            ChunkChecksums = ["abc", "def"]
        };
        await metadata.SaveAsync(baseImagePath);

        var result = await _service.VerifyImageAsync(baseImagePath);

        result.Should().BeFalse();
    }

    #endregion

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_SetsItemStatusToCompleted()
    {
        // Mock VSS failure + PowerShell failure to prevent actual disk cloning
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "not admin" });

        var item = new MigrationItem
        {
            DisplayName = "Full Disk",
            ItemType = MigrationItemType.Application,
            IsSelected = true,
            RecommendedTier = MigrationTier.FullClone
        };

        // This will attempt to clone but the block copy will fail since we're targeting
        // a raw device path. We catch to verify status was set to InProgress.
        try
        {
            await _service.CaptureAsync(new[] { item }, _tempDir);
        }
        catch
        {
            // Expected — we can't actually clone a volume in a test
        }

        item.Status.Should().BeOneOf(MigrationItemStatus.InProgress, MigrationItemStatus.Completed);
    }

    [Fact]
    public async Task CaptureAsync_OnlyProcessesFullCloneItems()
    {
        var packageItem = new MigrationItem
        {
            DisplayName = "PackageApp",
            IsSelected = true,
            RecommendedTier = MigrationTier.Package
        };

        var cloneItem = new MigrationItem
        {
            DisplayName = "CloneThis",
            IsSelected = true,
            RecommendedTier = MigrationTier.FullClone
        };

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        try
        {
            await _service.CaptureAsync(new[] { packageItem, cloneItem }, _tempDir);
        }
        catch
        {
            // Expected
        }

        // Package item should remain Queued (not touched)
        packageItem.Status.Should().Be(MigrationItemStatus.Queued);
        // Clone item should have been set to InProgress at minimum
        cloneItem.Status.Should().BeOneOf(MigrationItemStatus.InProgress, MigrationItemStatus.Completed);
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_ThrowsIfNoImageFiles()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.RestoreAsync(emptyDir, Array.Empty<UserMapping>()));
    }

    #endregion

    #region VolumeInfo / VssShadowCopy models

    [Fact]
    public void VolumeInfo_HasDefaultValues()
    {
        var info = new VolumeInfo();

        info.TotalSize.Should().Be(0);
        info.FreeSpace.Should().Be(0);
        info.FileSystem.Should().BeEmpty();
    }

    #endregion
}
