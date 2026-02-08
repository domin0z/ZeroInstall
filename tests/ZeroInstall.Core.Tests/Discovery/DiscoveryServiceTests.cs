using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Tests.Discovery;

public class DiscoveryServiceTests
{
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();

    private DiscoveryService CreateService()
    {
        var appDiscovery = new ApplicationDiscoveryService(
            _registry, _fileSystem, _processRunner,
            NullLogger<ApplicationDiscoveryService>.Instance);
        var profileDiscovery = new UserProfileDiscoveryService(
            _registry, _fileSystem,
            NullLogger<UserProfileDiscoveryService>.Instance);
        var settingsDiscovery = new SystemSettingsDiscoveryService(
            _registry, _processRunner,
            NullLogger<SystemSettingsDiscoveryService>.Instance);

        return new DiscoveryService(
            appDiscovery, profileDiscovery, settingsDiscovery,
            NullLogger<DiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAllAsync_AggregatesAllSources()
    {
        // Setup: 1 app, 1 user profile, 1 printer
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("Uninstall")))
            .Returns(new[] { "TestApp" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default,
            Arg.Is<string>(s => s.Contains("Uninstall")))
            .Returns(Array.Empty<string>());

        // App setup
        var appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TestApp";
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64, appPath, "DisplayName")
            .Returns("Test Application");
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64, appPath, "DisplayVersion")
            .Returns("1.0");
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64, appPath, "Publisher")
            .Returns("Test Vendor");
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64, appPath, Arg.Is<string>(s =>
            s != "DisplayName" && s != "DisplayVersion" && s != "Publisher"))
            .Returns((string?)null);
        _registry.GetDwordValue(RegistryHive.LocalMachine, RegistryView.Registry64, appPath, Arg.Any<string>())
            .Returns((int?)null);

        // Profile setup
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList")
            .Returns(new[] { "S-1-5-21-111-222-333-1001" });
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("ProfileList")), "ProfileImagePath")
            .Returns(@"C:\Users\TestUser");
        _fileSystem.DirectoryExists(@"C:\Users\TestUser").Returns(true);
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.StartsWith(@"C:\Users\TestUser\"))).Returns(false);

        // Printer setup
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts")
            .Returns(new[] { "Office Printer" });
        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts", Arg.Any<string>())
            .Returns("data");

        // Other settings - return empty
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, @"Network")
            .Returns(Array.Empty<string>());
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, @"Network")
            .Returns(Array.Empty<string>());
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, @"Environment")
            .Returns(Array.Empty<string>());
        _registry.GetValueNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts")
            .Returns(Array.Empty<string>());

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var service = CreateService();
        var items = await service.DiscoverAllAsync();

        // Should have: 1 app + 1 profile + 1 printer = 3 items minimum
        items.Should().HaveCountGreaterOrEqualTo(3);

        items.Should().Contain(i => i.ItemType == MigrationItemType.Application);
        items.Should().Contain(i => i.ItemType == MigrationItemType.UserProfile);
        items.Should().Contain(i => i.ItemType == MigrationItemType.SystemSetting);
    }

    [Fact]
    public void FormatBytes_FormatsCorrectly()
    {
        DiscoveryService.FormatBytes(500).Should().Be("500 B");
        DiscoveryService.FormatBytes(1536).Should().Be("1.5 KB");
        DiscoveryService.FormatBytes(5_242_880).Should().Be("5.0 MB");
        DiscoveryService.FormatBytes(2_684_354_560).Should().Be("2.50 GB");
    }
}
