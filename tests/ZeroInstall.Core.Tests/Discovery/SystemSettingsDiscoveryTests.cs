using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Discovery;

public class SystemSettingsDiscoveryTests
{
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly SystemSettingsDiscoveryService _service;

    public SystemSettingsDiscoveryTests()
    {
        _service = new SystemSettingsDiscoveryService(
            _registry, _processRunner,
            NullLogger<SystemSettingsDiscoveryService>.Instance);
    }

    [Fact]
    public void DiscoverPrinters_FindsInstalledPrinters()
    {
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts")
            .Returns(new[] { "HP LaserJet", "Microsoft Print to PDF" });

        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts", Arg.Any<string>())
            .Returns("winspool,Ne00:,15,45");

        var printers = _service.DiscoverPrinters();

        printers.Should().HaveCount(2);
        printers[0].Name.Should().Be("HP LaserJet");
        printers[0].Category.Should().Be(SystemSettingCategory.Printer);
        printers[1].Name.Should().Be("Microsoft Print to PDF");
    }

    [Fact]
    public async Task DiscoverWifiProfilesAsync_ParsesNetshOutput()
    {
        var netshOutput = """
            Profiles on interface Wi-Fi:

            Group policy profiles (read only)
            ---------------------------------
                <None>

            User profiles
            -------------
                All User Profile     : HomeNetwork
                All User Profile     : OfficeWiFi
            """;

        _processRunner.RunAsync("netsh", "wlan show profiles", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = netshOutput });

        _processRunner.RunAsync("netsh", Arg.Is<string>(s => s.Contains("show profile name=")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "profile data here" });

        var profiles = await _service.DiscoverWifiProfilesAsync(default);

        profiles.Should().HaveCount(2);
        profiles[0].Name.Should().Be("HomeNetwork");
        profiles[0].Category.Should().Be(SystemSettingCategory.WifiProfile);
        profiles[1].Name.Should().Be("OfficeWiFi");
    }

    [Fact]
    public void DiscoverMappedDrives_FindsMappedDrives()
    {
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, @"Network")
            .Returns(new[] { "Z", "Y" });
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, @"Network")
            .Returns(Array.Empty<string>());

        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Network\Z", "RemotePath")
            .Returns(@"\\NAS\SharedDrive");
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Network\Y", "RemotePath")
            .Returns(@"\\Server\Data");

        var drives = _service.DiscoverMappedDrives();

        drives.Should().HaveCount(2);
        drives[0].Category.Should().Be(SystemSettingCategory.MappedDrive);
        drives[0].Data.Should().Be(@"\\NAS\SharedDrive");
    }

    [Fact]
    public void DiscoverEnvironmentVariables_FindsUserVars()
    {
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, @"Environment")
            .Returns(new[] { "JAVA_HOME", "CUSTOM_VAR" });

        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Environment", "JAVA_HOME")
            .Returns(@"C:\Java\jdk-21");
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Environment", "CUSTOM_VAR")
            .Returns("some_value");

        // System vars - return only standard ones that should be skipped
        _registry.GetValueNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")
            .Returns(new[] { "Path", "ComSpec", "OS" });

        var envVars = _service.DiscoverEnvironmentVariables();

        // Should only have the 2 user vars (system standard vars are filtered out)
        envVars.Should().HaveCount(2);
        envVars[0].Name.Should().Contain("JAVA_HOME");
        envVars[0].Category.Should().Be(SystemSettingCategory.EnvironmentVariable);
    }

    [Fact]
    public void DiscoverDefaultAppAssociations_CountsExtensions()
    {
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts")
            .Returns(new[] { ".txt", ".pdf", ".jpg", ".noassoc" });

        // Only 3 have UserChoice ProgId
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            Arg.Is<string>(s => s.Contains(@".txt\UserChoice")), "ProgId")
            .Returns("txtfile");
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            Arg.Is<string>(s => s.Contains(@".pdf\UserChoice")), "ProgId")
            .Returns("AcroExch.Document");
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            Arg.Is<string>(s => s.Contains(@".jpg\UserChoice")), "ProgId")
            .Returns("PhotoViewer");
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            Arg.Is<string>(s => s.Contains(@".noassoc\UserChoice")), "ProgId")
            .Returns((string?)null);

        var assocs = _service.DiscoverDefaultAppAssociations();

        assocs.Should().HaveCount(1);
        assocs[0].Category.Should().Be(SystemSettingCategory.DefaultAppAssociation);
        assocs[0].Data.Should().Be("3");
    }
}
