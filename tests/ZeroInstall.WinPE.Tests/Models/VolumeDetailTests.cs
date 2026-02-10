using ZeroInstall.Core.Models;

namespace ZeroInstall.WinPE.Tests.Models;

public class VolumeDetailTests
{
    [Fact]
    public void VolumeDetail_DefaultValues_AreCorrect()
    {
        var vol = new VolumeDetail();

        vol.DriveLetter.Should().BeEmpty();
        vol.Label.Should().BeEmpty();
        vol.FileSystem.Should().BeEmpty();
        vol.SizeBytes.Should().Be(0);
        vol.FreeSpaceBytes.Should().Be(0);
        vol.DiskNumber.Should().Be(0);
        vol.VolumeType.Should().BeEmpty();
        vol.HealthStatus.Should().BeEmpty();
    }

    [Fact]
    public void VolumeDetail_PropertyAssignment_WorksCorrectly()
    {
        var vol = new VolumeDetail
        {
            DriveLetter = "C",
            Label = "Windows",
            FileSystem = "NTFS",
            SizeBytes = 499_999_997_952L,
            FreeSpaceBytes = 200_000_000_000L,
            DiskNumber = 0,
            VolumeType = "Partition",
            HealthStatus = "Healthy"
        };

        vol.DriveLetter.Should().Be("C");
        vol.Label.Should().Be("Windows");
        vol.FileSystem.Should().Be("NTFS");
        vol.SizeBytes.Should().Be(499_999_997_952L);
        vol.FreeSpaceBytes.Should().Be(200_000_000_000L);
        vol.DiskNumber.Should().Be(0);
        vol.VolumeType.Should().Be("Partition");
        vol.HealthStatus.Should().Be("Healthy");
    }

    [Fact]
    public void VolumeDetail_LargeFreeSpace_HandledCorrectly()
    {
        var vol = new VolumeDetail
        {
            SizeBytes = 2_000_398_934_016L,
            FreeSpaceBytes = 1_800_000_000_000L
        };

        vol.FreeSpaceBytes.Should().BeLessThanOrEqualTo(vol.SizeBytes);
    }

    [Fact]
    public void VolumeDetail_EmptyDriveLetter_Accepted()
    {
        var vol = new VolumeDetail
        {
            DriveLetter = string.Empty,
            Label = "Recovery",
            FileSystem = "NTFS"
        };

        vol.DriveLetter.Should().BeEmpty();
        vol.Label.Should().Be("Recovery");
    }
}
