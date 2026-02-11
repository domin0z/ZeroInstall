using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class FirmwareServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly FirmwareService _service;

    public FirmwareServiceTests()
    {
        _service = new FirmwareService(_processRunner, NullLogger<FirmwareService>.Instance);
    }

    #region Model Defaults

    [Fact]
    public void FirmwareInfo_DefaultValues()
    {
        var info = new FirmwareInfo();

        info.FirmwareType.Should().Be(FirmwareType.Unknown);
        info.SecureBoot.Should().Be(SecureBootStatus.Unknown);
        info.TpmPresent.Should().BeFalse();
        info.TpmVersion.Should().BeEmpty();
        info.BiosVendor.Should().BeEmpty();
        info.BiosVersion.Should().BeEmpty();
        info.BiosReleaseDate.Should().BeEmpty();
        info.SystemManufacturer.Should().BeEmpty();
        info.SystemModel.Should().BeEmpty();
        info.BootEntries.Should().BeEmpty();
    }

    [Fact]
    public void FirmwareInfo_SetProperties()
    {
        var info = new FirmwareInfo
        {
            FirmwareType = FirmwareType.Uefi,
            SecureBoot = SecureBootStatus.Enabled,
            TpmPresent = true,
            TpmVersion = "2.0",
            BiosVendor = "AMI",
            BiosVersion = "1.0",
            BiosReleaseDate = "2024-01-15",
            SystemManufacturer = "Dell",
            SystemModel = "OptiPlex 7090",
            BootEntries = [new BcdBootEntry { Identifier = "{current}" }]
        };

        info.FirmwareType.Should().Be(FirmwareType.Uefi);
        info.SecureBoot.Should().Be(SecureBootStatus.Enabled);
        info.TpmPresent.Should().BeTrue();
        info.TpmVersion.Should().Be("2.0");
        info.BiosVendor.Should().Be("AMI");
        info.BiosVersion.Should().Be("1.0");
        info.BiosReleaseDate.Should().Be("2024-01-15");
        info.SystemManufacturer.Should().Be("Dell");
        info.SystemModel.Should().Be("OptiPlex 7090");
        info.BootEntries.Should().HaveCount(1);
    }

    [Fact]
    public void BcdBootEntry_DefaultValues()
    {
        var entry = new BcdBootEntry();

        entry.Identifier.Should().BeEmpty();
        entry.EntryType.Should().BeEmpty();
        entry.Description.Should().BeEmpty();
        entry.Device.Should().BeEmpty();
        entry.Path.Should().BeEmpty();
        entry.IsDefault.Should().BeFalse();
        entry.Properties.Should().BeEmpty();
    }

    [Fact]
    public void BcdBootEntry_PropertiesDict()
    {
        var entry = new BcdBootEntry
        {
            Identifier = "{current}",
            EntryType = "Windows Boot Loader",
            Description = "Windows 10",
            Device = "partition=C:",
            Path = @"\Windows\system32\winload.efi",
            IsDefault = true,
            Properties = new Dictionary<string, string>
            {
                ["locale"] = "en-US",
                ["osdevice"] = "partition=C:"
            }
        };

        entry.Identifier.Should().Be("{current}");
        entry.Properties.Should().HaveCount(2);
        entry.Properties["locale"].Should().Be("en-US");
    }

    #endregion

    #region Enum Existence

    [Fact]
    public void FirmwareType_UefiExists()
    {
        FirmwareType.Uefi.Should().BeDefined();
    }

    [Fact]
    public void SecureBootStatus_EnabledExists()
    {
        SecureBootStatus.Enabled.Should().BeDefined();
    }

    #endregion

    #region ParseFirmwareType

    [Fact]
    public void ParseFirmwareType_Two_ReturnsUefi()
    {
        FirmwareService.ParseFirmwareType(2).Should().Be(FirmwareType.Uefi);
    }

    [Fact]
    public void ParseFirmwareType_One_ReturnsBios()
    {
        FirmwareService.ParseFirmwareType(1).Should().Be(FirmwareType.Bios);
    }

    [Fact]
    public void ParseFirmwareType_Null_ReturnsUnknown()
    {
        FirmwareService.ParseFirmwareType(null).Should().Be(FirmwareType.Unknown);
    }

    #endregion

    #region ParseSecureBootStatus

    [Fact]
    public void ParseSecureBootStatus_True_ReturnsEnabled()
    {
        FirmwareService.ParseSecureBootStatus("True").Should().Be(SecureBootStatus.Enabled);
    }

    [Fact]
    public void ParseSecureBootStatus_False_ReturnsDisabled()
    {
        FirmwareService.ParseSecureBootStatus("False").Should().Be(SecureBootStatus.Disabled);
    }

    [Fact]
    public void ParseSecureBootStatus_NotSupported_ReturnsNotSupported()
    {
        FirmwareService.ParseSecureBootStatus("NotSupported").Should().Be(SecureBootStatus.NotSupported);
    }

    [Fact]
    public void ParseSecureBootStatus_Empty_ReturnsUnknown()
    {
        FirmwareService.ParseSecureBootStatus("").Should().Be(SecureBootStatus.Unknown);
    }

    #endregion

    #region ParseBiosInfo

    [Fact]
    public void ParseBiosInfo_ValidJson_ReturnsValues()
    {
        var json = """{"Manufacturer": "American Megatrends Inc.", "SMBIOSBIOSVersion": "1.80", "ReleaseDate": "2023-06-15"}""";
        using var doc = JsonDocument.Parse(json);

        var (vendor, version, releaseDate) = FirmwareService.ParseBiosInfo(doc.RootElement);

        vendor.Should().Be("American Megatrends Inc.");
        version.Should().Be("1.80");
        releaseDate.Should().Be("2023-06-15");
    }

    [Fact]
    public void ParseBiosInfo_NullElement_ReturnsDefaults()
    {
        var json = "null";
        using var doc = JsonDocument.Parse(json);

        var (vendor, version, releaseDate) = FirmwareService.ParseBiosInfo(doc.RootElement);

        vendor.Should().BeEmpty();
        version.Should().BeEmpty();
        releaseDate.Should().BeEmpty();
    }

    #endregion

    #region ParseSystemInfo

    [Fact]
    public void ParseSystemInfo_ValidJson_ReturnsValues()
    {
        var json = """{"Manufacturer": "Dell Inc.", "Model": "OptiPlex 7090"}""";
        using var doc = JsonDocument.Parse(json);

        var (manufacturer, model) = FirmwareService.ParseSystemInfo(doc.RootElement);

        manufacturer.Should().Be("Dell Inc.");
        model.Should().Be("OptiPlex 7090");
    }

    [Fact]
    public void ParseSystemInfo_NullElement_ReturnsDefaults()
    {
        var json = "null";
        using var doc = JsonDocument.Parse(json);

        var (manufacturer, model) = FirmwareService.ParseSystemInfo(doc.RootElement);

        manufacturer.Should().BeEmpty();
        model.Should().BeEmpty();
    }

    #endregion

    #region ParseTpmInfo

    [Fact]
    public void ParseTpmInfo_ValidJson_ReturnsPresentAndVersion()
    {
        var json = """{"IsActivated_InitialValue": true, "SpecVersion": "2.0, 0, 1.38"}""";
        using var doc = JsonDocument.Parse(json);

        var (present, version) = FirmwareService.ParseTpmInfo(doc.RootElement);

        present.Should().BeTrue();
        version.Should().Be("2.0");
    }

    [Fact]
    public void ParseTpmInfo_NullElement_ReturnsNotPresent()
    {
        var json = "null";
        using var doc = JsonDocument.Parse(json);

        var (present, version) = FirmwareService.ParseTpmInfo(doc.RootElement);

        present.Should().BeFalse();
        version.Should().BeEmpty();
    }

    #endregion

    #region ParseBcdEnum

    [Fact]
    public void ParseBcdEnum_MultipleEntries_ParsesCorrectly()
    {
        var output = """
            Windows Boot Manager
            --------------------
            identifier              {bootmgr}
            device                  partition=\Device\HarddiskVolume1
            description             Windows Boot Manager
            locale                  en-US

            Windows Boot Loader
            -------------------
            identifier              {current}
            device                  partition=C:
            path                    \Windows\system32\winload.efi
            description             Windows 10
            locale                  en-US
            osdevice                partition=C:

            Windows Boot Loader
            -------------------
            identifier              {7a29fa4c-55c7-11eb-b6d4-e86a64c7b4a0}
            device                  partition=D:
            path                    \Windows\system32\winload.efi
            description             Windows Recovery
            """;

        var entries = FirmwareService.ParseBcdEnum(output);

        entries.Should().HaveCount(3);

        entries[0].EntryType.Should().Be("Windows Boot Manager");
        entries[0].Identifier.Should().Be("{bootmgr}");
        entries[0].Description.Should().Be("Windows Boot Manager");
        entries[0].IsDefault.Should().BeFalse();

        entries[1].EntryType.Should().Be("Windows Boot Loader");
        entries[1].Identifier.Should().Be("{current}");
        entries[1].Device.Should().Be("partition=C:");
        entries[1].Path.Should().Be(@"\Windows\system32\winload.efi");
        entries[1].Description.Should().Be("Windows 10");
        entries[1].IsDefault.Should().BeTrue();
        entries[1].Properties.Should().ContainKey("locale");

        entries[2].Identifier.Should().Be("{7a29fa4c-55c7-11eb-b6d4-e86a64c7b4a0}");
        entries[2].Description.Should().Be("Windows Recovery");
    }

    [Fact]
    public void ParseBcdEnum_SingleEntry_ParsesCorrectly()
    {
        var output = """
            Windows Boot Loader
            -------------------
            identifier              {current}
            device                  partition=C:
            description             Windows 11
            """;

        var entries = FirmwareService.ParseBcdEnum(output);

        entries.Should().HaveCount(1);
        entries[0].Identifier.Should().Be("{current}");
        entries[0].Description.Should().Be("Windows 11");
        entries[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ParseBcdEnum_EmptyOutput_ReturnsEmptyList()
    {
        var entries = FirmwareService.ParseBcdEnum("");

        entries.Should().BeEmpty();
    }

    #endregion

    #region GetFirmwareInfoAsync

    [Fact]
    public async Task GetFirmwareInfoAsync_Success_PopulatesInfo()
    {
        var wmiJson = """
            {
              "Bios": {"Manufacturer": "AMI", "SMBIOSBIOSVersion": "2.10", "ReleaseDate": "2024-01-01"},
              "System": {"Manufacturer": "Lenovo", "Model": "ThinkPad T14s"},
              "FirmwareType": 2,
              "SecureBoot": "True",
              "Tpm": {"IsActivated_InitialValue": true, "SpecVersion": "2.0, 0, 1.38"}
            }
            """;

        var bcdOutput = """
            Windows Boot Loader
            -------------------
            identifier              {current}
            device                  partition=C:
            description             Windows 11
            """;

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = wmiJson });

        _processRunner.RunAsync("bcdedit", "/enum all", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = bcdOutput });

        var info = await _service.GetFirmwareInfoAsync();

        info.FirmwareType.Should().Be(FirmwareType.Uefi);
        info.SecureBoot.Should().Be(SecureBootStatus.Enabled);
        info.TpmPresent.Should().BeTrue();
        info.TpmVersion.Should().Be("2.0");
        info.BiosVendor.Should().Be("AMI");
        info.SystemManufacturer.Should().Be("Lenovo");
        info.SystemModel.Should().Be("ThinkPad T14s");
        info.BootEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetFirmwareInfoAsync_PowerShellFails_ReturnsDefaultsWithBootEntries()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "PowerShell not found" });

        _processRunner.RunAsync("bcdedit", "/enum all", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Windows Boot Loader
                    -------------------
                    identifier              {current}
                    description             Windows 10
                    """
            });

        var info = await _service.GetFirmwareInfoAsync();

        info.FirmwareType.Should().Be(FirmwareType.Unknown);
        info.BootEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetFirmwareInfoAsync_Exception_ReturnsDefaults()
    {
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("process error"));

        var info = await _service.GetFirmwareInfoAsync();

        info.FirmwareType.Should().Be(FirmwareType.Unknown);
        info.BootEntries.Should().BeEmpty();
    }

    #endregion

    #region ExportBcdAsync

    [Fact]
    public async Task ExportBcdAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("bcdedit", Arg.Is<string>(s => s.Contains("/export")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.ExportBcdAsync(@"E:\backup\bcd_store");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("bcdedit",
            Arg.Is<string>(s => s.Contains("/export") && s.Contains("bcd_store")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportBcdAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("bcdedit", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var result = await _service.ExportBcdAsync(@"E:\backup\bcd_store");

        result.Should().BeFalse();
    }

    #endregion

    #region ImportBcdAsync

    [Fact]
    public async Task ImportBcdAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("bcdedit", Arg.Is<string>(s => s.Contains("/import")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.ImportBcdAsync(@"E:\backup\bcd_store");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("bcdedit",
            Arg.Is<string>(s => s.Contains("/import") && s.Contains("bcd_store")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetBootEntriesAsync

    [Fact]
    public async Task GetBootEntriesAsync_Success_ReturnsEntries()
    {
        _processRunner.RunAsync("bcdedit", "/enum all", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Windows Boot Loader
                    -------------------
                    identifier              {current}
                    description             Windows 10
                    """
            });

        var entries = await _service.GetBootEntriesAsync();

        entries.Should().HaveCount(1);
        entries[0].Identifier.Should().Be("{current}");
    }

    [Fact]
    public async Task GetBootEntriesAsync_Failure_ReturnsEmpty()
    {
        _processRunner.RunAsync("bcdedit", "/enum all", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "bcdedit not found" });

        var entries = await _service.GetBootEntriesAsync();

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBootEntriesAsync_Exception_ReturnsEmpty()
    {
        _processRunner.RunAsync("bcdedit", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("error"));

        var entries = await _service.GetBootEntriesAsync();

        entries.Should().BeEmpty();
    }

    #endregion
}
