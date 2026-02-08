using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Tests.Discovery;

public class ApplicationDiscoveryTests
{
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly ApplicationDiscoveryService _service;

    public ApplicationDiscoveryTests()
    {
        _service = new ApplicationDiscoveryService(
            _registry, _fileSystem, _processRunner,
            NullLogger<ApplicationDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverAsync_FindsAppsFromRegistry()
    {
        // Arrange: 64-bit HKLM has two apps
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, Arg.Any<string>())
            .Returns(new[] { "App1", "App2" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, Arg.Any<string>())
            .Returns(Array.Empty<string>());

        SetupApp("App1", RegistryHive.LocalMachine, RegistryView.Registry64, "Google Chrome", "120.0", "Google");
        SetupApp("App2", RegistryHive.LocalMachine, RegistryView.Registry64, "7-Zip", "23.01", "Igor Pavlov");

        _processRunner.RunAsync("winget", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });
        _processRunner.RunAsync("choco", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        // Act
        var apps = await _service.DiscoverAsync();

        // Assert
        apps.Should().HaveCount(2);
        apps[0].Name.Should().Be("Google Chrome");
        apps[0].Publisher.Should().Be("Google");
        apps[1].Name.Should().Be("7-Zip");
    }

    [Fact]
    public async Task DiscoverAsync_SkipsSystemComponents()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, Arg.Any<string>())
            .Returns(new[] { "SystemApp", "RealApp" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, Arg.Any<string>())
            .Returns(Array.Empty<string>());

        SetupApp("SystemApp", RegistryHive.LocalMachine, RegistryView.Registry64, "System Thing", "1.0", "MS", systemComponent: 1);
        SetupApp("RealApp", RegistryHive.LocalMachine, RegistryView.Registry64, "Real App", "2.0", "Vendor");

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var apps = await _service.DiscoverAsync();

        apps.Should().HaveCount(1);
        apps[0].Name.Should().Be("Real App");
    }

    [Fact]
    public async Task DiscoverAsync_SkipsEntriesWithNoDisplayName()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, Arg.Any<string>())
            .Returns(new[] { "NoName", "HasName" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, Arg.Any<string>())
            .Returns(Array.Empty<string>());

        // NoName has null DisplayName
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("NoName")), "DisplayName")
            .Returns((string?)null);

        SetupApp("HasName", RegistryHive.LocalMachine, RegistryView.Registry64, "Real App", "1.0", "Vendor");

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var apps = await _service.DiscoverAsync();

        apps.Should().HaveCount(1);
        apps[0].Name.Should().Be("Real App");
    }

    [Fact]
    public async Task DiscoverAsync_EnrichesWithWinget()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, Arg.Any<string>())
            .Returns(new[] { "Chrome" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, Arg.Any<string>())
            .Returns(Array.Empty<string>());

        SetupApp("Chrome", RegistryHive.LocalMachine, RegistryView.Registry64, "Google Chrome", "120.0", "Google");

        var wingetOutput = """
            Name                            Id                      Version
            ----------------------------------------------------------------
            Google Chrome                   Google.Chrome           120.0.6099
            """;

        _processRunner.RunAsync("winget", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = wingetOutput });
        _processRunner.RunAsync("choco", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var apps = await _service.DiscoverAsync();

        apps.Should().HaveCount(1);
        apps[0].WingetPackageId.Should().Be("Google.Chrome");
        apps[0].RecommendedTier.Should().Be(MigrationTier.Package);
    }

    [Fact]
    public void ParseWingetOutput_ParsesCorrectly()
    {
        var output =
            "Name                            Id                      Version     Source\n" +
            "---------------------------------------------------------------------------------\n" +
            "Google Chrome                   Google.Chrome           120.0.6099  winget\n" +
            "7-Zip 23.01 (x64)               7zip.7zip               23.01       winget\n";

        var entries = ApplicationDiscoveryService.ParseWingetOutput(output);

        entries.Should().HaveCount(2);
        entries[0].Name.Should().Be("Google Chrome");
        entries[0].PackageId.Should().Be("Google.Chrome");
        entries[1].PackageId.Should().Be("7zip.7zip");
    }

    [Fact]
    public void ParseChocolateyOutput_ParsesCorrectly()
    {
        var output = """
            7zip|23.01
            googlechrome|120.0.6099
            firefox|121.0
            """;

        var entries = ApplicationDiscoveryService.ParseChocolateyOutput(output);

        entries.Should().HaveCount(3);
        entries[0].PackageId.Should().Be("7zip");
        entries[0].Version.Should().Be("23.01");
        entries[1].PackageId.Should().Be("googlechrome");
        entries[2].PackageId.Should().Be("firefox");
    }

    [Fact]
    public async Task DiscoverAsync_UsesRegistryEstimatedSizeWhenNoInstallLocation()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, Arg.Any<string>())
            .Returns(new[] { "App1" });
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _registry.GetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, Arg.Any<string>())
            .Returns(Array.Empty<string>());

        SetupApp("App1", RegistryHive.LocalMachine, RegistryView.Registry64, "SomeApp", "1.0", "Vendor");

        // No install location, but registry has EstimatedSize (in KB)
        _registry.GetDwordValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("App1")), "EstimatedSize")
            .Returns(50000); // 50000 KB

        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var apps = await _service.DiscoverAsync();

        apps.Should().HaveCount(1);
        apps[0].EstimatedSizeBytes.Should().Be(50000 * 1024L);
    }

    private void SetupApp(string subKeyName, RegistryHive hive, RegistryView view,
        string displayName, string version, string publisher, int? systemComponent = null)
    {
        var keyPath = view == RegistryView.Registry32
            ? @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        var fullPath = $@"{keyPath}\{subKeyName}";

        _registry.GetStringValue(hive, view, fullPath, "DisplayName").Returns(displayName);
        _registry.GetStringValue(hive, view, fullPath, "DisplayVersion").Returns(version);
        _registry.GetStringValue(hive, view, fullPath, "Publisher").Returns(publisher);
        _registry.GetStringValue(hive, view, fullPath, "InstallLocation").Returns((string?)null);
        _registry.GetStringValue(hive, view, fullPath, "UninstallString").Returns((string?)null);
        _registry.GetStringValue(hive, view, fullPath, "ParentKeyName").Returns((string?)null);
        _registry.GetDwordValue(hive, view, fullPath, "SystemComponent").Returns(systemComponent);
    }
}
