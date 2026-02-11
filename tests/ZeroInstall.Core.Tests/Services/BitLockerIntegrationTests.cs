using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Services;

public class BitLockerIntegrationTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IBitLockerService _bitLockerService = Substitute.For<IBitLockerService>();

    #region VolumeDetail Model

    [Fact]
    public void VolumeDetail_IsBitLockerEncrypted_TrueForUnlocked()
    {
        var volume = new VolumeDetail { BitLockerStatus = BitLockerProtectionStatus.Unlocked };
        volume.IsBitLockerEncrypted.Should().BeTrue();
    }

    [Fact]
    public void VolumeDetail_IsBitLockerEncrypted_TrueForLocked()
    {
        var volume = new VolumeDetail { BitLockerStatus = BitLockerProtectionStatus.Locked };
        volume.IsBitLockerEncrypted.Should().BeTrue();
    }

    [Fact]
    public void VolumeDetail_IsBitLockerEncrypted_TrueForSuspended()
    {
        var volume = new VolumeDetail { BitLockerStatus = BitLockerProtectionStatus.Suspended };
        volume.IsBitLockerEncrypted.Should().BeTrue();
    }

    [Fact]
    public void VolumeDetail_IsBitLockerEncrypted_FalseForNotProtected()
    {
        var volume = new VolumeDetail { BitLockerStatus = BitLockerProtectionStatus.NotProtected };
        volume.IsBitLockerEncrypted.Should().BeFalse();
    }

    [Fact]
    public void VolumeDetail_IsBitLockerEncrypted_FalseForUnknown()
    {
        var volume = new VolumeDetail { BitLockerStatus = BitLockerProtectionStatus.Unknown };
        volume.IsBitLockerEncrypted.Should().BeFalse();
    }

    #endregion

    #region DiskImageMetadata BitLocker Fields

    [Fact]
    public void DiskImageMetadata_BitLockerFields_DefaultValues()
    {
        var metadata = new DiskImageMetadata();

        metadata.SourceWasBitLockerEncrypted.Should().BeFalse();
        metadata.SourceBitLockerStatus.Should().BeEmpty();
        metadata.BitLockerWasSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task DiskImageMetadata_BitLockerFields_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-meta-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var imagePath = Path.Combine(tempDir, "test.img");
            await File.WriteAllBytesAsync(imagePath, new byte[10]);

            var metadata = new DiskImageMetadata
            {
                SourceWasBitLockerEncrypted = true,
                SourceBitLockerStatus = "Unlocked",
                BitLockerWasSuspended = true
            };
            await metadata.SaveAsync(imagePath);

            var loaded = await DiskImageMetadata.LoadAsync(imagePath);

            loaded.Should().NotBeNull();
            loaded!.SourceWasBitLockerEncrypted.Should().BeTrue();
            loaded.SourceBitLockerStatus.Should().Be("Unlocked");
            loaded.BitLockerWasSuspended.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region DiskEnumerationService + BitLocker

    [Fact]
    public async Task GetVolumesAsync_PopulatesBitLockerStatus()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """[{ "DriveLetter": "C", "FileSystem": "NTFS", "Size": 500 }]"""
            });

        _bitLockerService.GetStatusAsync("C:", Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.Unlocked,
                LockStatus = "Unlocked"
            });

        var service = new DiskEnumerationService(
            _processRunner, NullLogger<DiskEnumerationService>.Instance, _bitLockerService);

        var volumes = await service.GetVolumesAsync();

        volumes.Should().HaveCount(1);
        volumes[0].BitLockerStatus.Should().Be(BitLockerProtectionStatus.Unlocked);
        volumes[0].BitLockerLockStatus.Should().Be("Unlocked");
        volumes[0].IsBitLockerEncrypted.Should().BeTrue();
    }

    [Fact]
    public async Task GetVolumesAsync_NullBitLockerService_StillWorks()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """[{ "DriveLetter": "C", "FileSystem": "NTFS", "Size": 500 }]"""
            });

        var service = new DiskEnumerationService(
            _processRunner, NullLogger<DiskEnumerationService>.Instance);

        var volumes = await service.GetVolumesAsync();

        volumes.Should().HaveCount(1);
        volumes[0].BitLockerStatus.Should().Be(BitLockerProtectionStatus.Unknown);
    }

    [Fact]
    public async Task GetVolumesAsync_BitLockerServiceFailure_GracefulFallback()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """[{ "DriveLetter": "C", "FileSystem": "NTFS", "Size": 500 }]"""
            });

        _bitLockerService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<BitLockerStatus>(_ => throw new InvalidOperationException("manage-bde not found"));

        var service = new DiskEnumerationService(
            _processRunner, NullLogger<DiskEnumerationService>.Instance, _bitLockerService);

        var volumes = await service.GetVolumesAsync();

        // Should still return volumes, just without BitLocker info
        volumes.Should().HaveCount(1);
        volumes[0].BitLockerStatus.Should().Be(BitLockerProtectionStatus.Unknown);
    }

    #endregion

    #region DiskClonerService + BitLocker

    [Fact]
    public async Task CloneVolumeAsync_ThrowsOnLockedVolume()
    {
        _bitLockerService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "D:",
                ProtectionStatus = BitLockerProtectionStatus.Locked,
                LockStatus = "Locked"
            });

        var service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance, _bitLockerService);

        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-clone-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var act = () => service.CloneVolumeAsync("D:", Path.Combine(tempDir, "out.img"), DiskImageFormat.Img);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*BitLocker-locked*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CloneVolumeAsync_DoesNotThrow_OnUnlockedVolume()
    {
        _bitLockerService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.Unlocked,
                LockStatus = "Unlocked"
            });

        // Make process runner return failure so we don't actually clone
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance, _bitLockerService);

        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-clone-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            // Should not throw — it proceeds past the BitLocker check
            // (will fail later in the clone process, but that's expected)
            try
            {
                await service.CloneVolumeAsync("C:", Path.Combine(tempDir, "out.img"), DiskImageFormat.Img);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Expected failure from actual cloning, not BitLocker
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CloneVolumeAsync_SucceedsOnSuspended()
    {
        _bitLockerService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.Suspended,
                LockStatus = "Unlocked"
            });

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance, _bitLockerService);

        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-clone-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            try
            {
                await service.CloneVolumeAsync("C:", Path.Combine(tempDir, "out.img"), DiskImageFormat.Img);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Expected failure from actual cloning, not BitLocker
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CloneVolumeAsync_SucceedsOnNotProtected()
    {
        _bitLockerService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.NotProtected,
                LockStatus = "Unlocked"
            });

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance, _bitLockerService);

        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-clone-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            try
            {
                await service.CloneVolumeAsync("C:", Path.Combine(tempDir, "out.img"), DiskImageFormat.Img);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Expected failure from actual cloning, not BitLocker
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CloneVolumeAsync_NullBitLockerService_BackwardCompat()
    {
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        // No BitLocker service — should work like before
        var service = new DiskClonerService(
            _processRunner, NullLogger<DiskClonerService>.Instance);

        var tempDir = Path.Combine(Path.GetTempPath(), "zim-bl-clone-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            try
            {
                await service.CloneVolumeAsync("C:", Path.Combine(tempDir, "out.img"), DiskImageFormat.Img);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Expected failure from actual cloning
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion
}
