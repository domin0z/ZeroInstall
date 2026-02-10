using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.WinPE.Tests.Discovery;

public class DiskEnumerationServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DiskEnumerationService _service;

    public DiskEnumerationServiceTests()
    {
        _service = new DiskEnumerationService(_processRunner, NullLogger<DiskEnumerationService>.Instance);
    }

    // --- ParseDiskJson Tests ---

    [Fact]
    public void ParseDiskJson_SingleObject_ReturnsSingleDisk()
    {
        var json = """
        {
            "Number": 0,
            "Model": "Samsung SSD 970 EVO",
            "Size": 500107862016,
            "PartitionStyle": "GPT",
            "IsOnline": true,
            "IsSystem": true,
            "IsBoot": true,
            "BusType": "NVMe"
        }
        """;

        var disks = DiskEnumerationService.ParseDiskJson(json);

        disks.Should().HaveCount(1);
        disks[0].Number.Should().Be(0);
        disks[0].Model.Should().Be("Samsung SSD 970 EVO");
        disks[0].SizeBytes.Should().Be(500107862016L);
        disks[0].PartitionStyle.Should().Be("GPT");
        disks[0].IsOnline.Should().BeTrue();
        disks[0].IsSystem.Should().BeTrue();
        disks[0].IsBoot.Should().BeTrue();
        disks[0].BusType.Should().Be("NVMe");
    }

    [Fact]
    public void ParseDiskJson_Array_ReturnsMultipleDisks()
    {
        var json = """
        [
            { "Number": 0, "Model": "SSD", "Size": 500107862016 },
            { "Number": 1, "Model": "HDD", "Size": 1000204886016 }
        ]
        """;

        var disks = DiskEnumerationService.ParseDiskJson(json);

        disks.Should().HaveCount(2);
        disks[0].Number.Should().Be(0);
        disks[0].Model.Should().Be("SSD");
        disks[1].Number.Should().Be(1);
        disks[1].Model.Should().Be("HDD");
    }

    [Fact]
    public void ParseDiskJson_EmptyString_ReturnsEmpty()
    {
        DiskEnumerationService.ParseDiskJson("").Should().BeEmpty();
    }

    [Fact]
    public void ParseDiskJson_InvalidJson_ReturnsEmpty()
    {
        DiskEnumerationService.ParseDiskJson("not json at all").Should().BeEmpty();
    }

    [Fact]
    public void ParseDiskJson_Null_ReturnsEmpty()
    {
        DiskEnumerationService.ParseDiskJson(null!).Should().BeEmpty();
    }

    [Fact]
    public void ParseDiskJson_NumericPartitionStyle_ParsedCorrectly()
    {
        var json = """{ "Number": 0, "PartitionStyle": 2, "BusType": 17 }""";

        var disks = DiskEnumerationService.ParseDiskJson(json);

        disks.Should().HaveCount(1);
        disks[0].PartitionStyle.Should().Be("GPT");
        disks[0].BusType.Should().Be("NVMe");
    }

    // --- ParseVolumeJson Tests ---

    [Fact]
    public void ParseVolumeJson_SingleObject_ReturnsSingleVolume()
    {
        var json = """
        {
            "DriveLetter": "C",
            "FileSystemLabel": "Windows",
            "FileSystem": "NTFS",
            "Size": 499999997952,
            "SizeRemaining": 200000000000,
            "DiskNumber": 0,
            "DriveType": "Fixed",
            "HealthStatus": "Healthy"
        }
        """;

        var volumes = DiskEnumerationService.ParseVolumeJson(json);

        volumes.Should().HaveCount(1);
        volumes[0].DriveLetter.Should().Be("C");
        volumes[0].Label.Should().Be("Windows");
        volumes[0].FileSystem.Should().Be("NTFS");
        volumes[0].SizeBytes.Should().Be(499999997952L);
        volumes[0].FreeSpaceBytes.Should().Be(200000000000L);
    }

    [Fact]
    public void ParseVolumeJson_Array_ReturnsMultipleVolumes()
    {
        var json = """
        [
            { "DriveLetter": "C", "FileSystemLabel": "Windows", "FileSystem": "NTFS", "Size": 500000000000 },
            { "DriveLetter": "D", "FileSystemLabel": "Data", "FileSystem": "NTFS", "Size": 1000000000000 }
        ]
        """;

        var volumes = DiskEnumerationService.ParseVolumeJson(json);

        volumes.Should().HaveCount(2);
        volumes[0].DriveLetter.Should().Be("C");
        volumes[1].DriveLetter.Should().Be("D");
    }

    [Fact]
    public void ParseVolumeJson_EmptyString_ReturnsEmpty()
    {
        DiskEnumerationService.ParseVolumeJson("").Should().BeEmpty();
    }

    [Fact]
    public void ParseVolumeJson_Null_ReturnsEmpty()
    {
        DiskEnumerationService.ParseVolumeJson(null!).Should().BeEmpty();
    }

    // --- Async method tests ---

    [Fact]
    public async Task GetDisksAsync_CallsPowerShell()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """[{ "Number": 0, "Model": "Test", "Size": 100 }]"""
            });

        var result = await _service.GetDisksAsync();

        result.Should().HaveCount(1);
        result[0].Model.Should().Be("Test");
        await _processRunner.Received(1).RunAsync("powershell", Arg.Is<string>(s => s.Contains("Get-Disk")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVolumesAsync_CallsPowerShell()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """[{ "DriveLetter": "C", "FileSystem": "NTFS", "Size": 500 }]"""
            });

        var result = await _service.GetVolumesAsync();

        result.Should().HaveCount(1);
        result[0].DriveLetter.Should().Be("C");
        await _processRunner.Received(1).RunAsync("powershell", Arg.Is<string>(s => s.Contains("Get-Volume")), Arg.Any<CancellationToken>());
    }
}
