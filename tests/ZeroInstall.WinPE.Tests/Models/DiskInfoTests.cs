using ZeroInstall.Core.Models;

namespace ZeroInstall.WinPE.Tests.Models;

public class DiskInfoTests
{
    [Fact]
    public void DiskInfo_DefaultValues_AreCorrect()
    {
        var disk = new DiskInfo();

        disk.Number.Should().Be(0);
        disk.Model.Should().BeEmpty();
        disk.SizeBytes.Should().Be(0);
        disk.PartitionStyle.Should().BeEmpty();
        disk.IsOnline.Should().BeFalse();
        disk.IsSystem.Should().BeFalse();
        disk.IsBoot.Should().BeFalse();
        disk.BusType.Should().BeEmpty();
    }

    [Fact]
    public void DiskInfo_PropertyAssignment_WorksCorrectly()
    {
        var disk = new DiskInfo
        {
            Number = 1,
            Model = "Samsung SSD 970 EVO Plus",
            SizeBytes = 500_107_862_016L,
            PartitionStyle = "GPT",
            IsOnline = true,
            IsSystem = true,
            IsBoot = true,
            BusType = "NVMe"
        };

        disk.Number.Should().Be(1);
        disk.Model.Should().Be("Samsung SSD 970 EVO Plus");
        disk.SizeBytes.Should().Be(500_107_862_016L);
        disk.PartitionStyle.Should().Be("GPT");
        disk.IsOnline.Should().BeTrue();
        disk.IsSystem.Should().BeTrue();
        disk.IsBoot.Should().BeTrue();
        disk.BusType.Should().Be("NVMe");
    }

    [Fact]
    public void DiskInfo_LargeSize_HandledCorrectly()
    {
        var disk = new DiskInfo
        {
            SizeBytes = 4_000_787_030_016L // ~4 TB
        };

        disk.SizeBytes.Should().Be(4_000_787_030_016L);
    }

    [Fact]
    public void DiskInfo_AllPartitionStyles_Accepted()
    {
        var gpt = new DiskInfo { PartitionStyle = "GPT" };
        var mbr = new DiskInfo { PartitionStyle = "MBR" };
        var raw = new DiskInfo { PartitionStyle = "RAW" };

        gpt.PartitionStyle.Should().Be("GPT");
        mbr.PartitionStyle.Should().Be("MBR");
        raw.PartitionStyle.Should().Be("RAW");
    }
}
