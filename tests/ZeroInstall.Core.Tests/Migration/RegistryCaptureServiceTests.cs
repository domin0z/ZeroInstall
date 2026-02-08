using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class RegistryCaptureServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly RegistryCaptureService _service;
    private readonly string _tempDir;

    public RegistryCaptureServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-regcap-test-" + Guid.NewGuid().ToString("N")[..8]);
        _service = new RegistryCaptureService(
            _processRunner,
            NullLogger<RegistryCaptureService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region ExportAsync

    [Fact]
    public async Task ExportAsync_WritesManifest()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "TestApp", Publisher = "TestPub", RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TestApp" }
        };

        var manifest = await _service.ExportAsync(apps, _tempDir);

        manifest.Entries.Should().NotBeEmpty();
        File.Exists(Path.Combine(_tempDir, "registry-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_SkipsFailedExports()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "The system was unable to find the specified registry key or value." });

        var apps = new List<DiscoveredApplication>
        {
            new() { Name = "NonExistent", RegistryKeyPath = @"SOFTWARE\NonExistent" }
        };

        var manifest = await _service.ExportAsync(apps, _tempDir);

        manifest.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_CallsRegExportForEachKey()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "MyApp",
                Publisher = "MyCorp",
                RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MyApp"
            }
        };

        await _service.ExportAsync(apps, _tempDir);

        await _processRunner.Received().RunAsync("reg",
            Arg.Is<string>(s => s.Contains("export")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_Handles32BitApps()
    {
        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var apps = new List<DiscoveredApplication>
        {
            new()
            {
                Name = "Old32BitApp",
                Publisher = "Vendor",
                Is32Bit = true,
                RegistryKeyPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Old32BitApp"
            }
        };

        var manifest = await _service.ExportAsync(apps, _tempDir);

        // Should include WOW6432Node keys
        manifest.Entries.Should().Contain(e =>
            e.RegistryKeyPath.Contains("WOW6432Node", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region ImportAsync

    [Fact]
    public async Task ImportAsync_ImportsRegFiles()
    {
        // Create a fake manifest and reg file
        Directory.CreateDirectory(_tempDir);

        var manifest = new RegistryCaptureManifest
        {
            Entries =
            [
                new RegistryExportEntry
                {
                    ApplicationName = "TestApp",
                    RegistryKeyPath = @"HKCU\Software\TestApp",
                    ExportFileName = "reg-0000.reg"
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "registry-manifest.json"), manifestJson);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "reg-0000.reg"), "Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\TestApp]\n\"Key\"=\"Value\"\n");

        _processRunner.RunAsync("reg", Arg.Is<string>(s => s.Contains("import")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.ImportAsync(_tempDir, new List<UserMapping>());

        await _processRunner.Received(1).RunAsync("reg",
            Arg.Is<string>(s => s.Contains("import")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_SkipsWhenNoManifest()
    {
        Directory.CreateDirectory(_tempDir);

        // Should not throw, just log a warning
        await _service.ImportAsync(_tempDir, new List<UserMapping>());

        await _processRunner.DidNotReceive().RunAsync("reg",
            Arg.Is<string>(s => s.Contains("import")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_RemapsUserPaths()
    {
        Directory.CreateDirectory(_tempDir);

        var manifest = new RegistryCaptureManifest
        {
            Entries =
            [
                new RegistryExportEntry
                {
                    ApplicationName = "TestApp",
                    RegistryKeyPath = @"HKCU\Software\TestApp",
                    ExportFileName = "reg-0000.reg"
                }
            ]
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "registry-manifest.json"), manifestJson);

        // .reg file with a user path that needs remapping (double-backslash format)
        var regContent = "Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\TestApp]\n\"DataPath\"=\"C:\\\\Users\\\\Bill\\\\AppData\\\\Roaming\\\\TestApp\"\n";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "reg-0000.reg"), regContent);

        _processRunner.RunAsync("reg", Arg.Is<string>(s => s.Contains("import")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        await _service.ImportAsync(_tempDir, mappings);

        // It should import a remapped temp file, not the original
        await _processRunner.Received(1).RunAsync("reg",
            Arg.Is<string>(s => s.Contains("import") && !s.Contains("reg-0000.reg")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region BuildKeyListForApp

    [Fact]
    public void BuildKeyListForApp_IncludesUninstallKey()
    {
        var app = new DiscoveredApplication
        {
            Name = "MyApp",
            RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MyApp"
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        keys.Should().Contain(k => k.Contains("Uninstall\\MyApp"));
    }

    [Fact]
    public void BuildKeyListForApp_IncludesPublisherAndNameKeys()
    {
        var app = new DiscoveredApplication
        {
            Name = "SuperEditor",
            Publisher = "CoolSoft"
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        keys.Should().Contain(@"HKLM\SOFTWARE\SuperEditor");
        keys.Should().Contain(@"HKCU\Software\SuperEditor");
        keys.Should().Contain(@"HKLM\SOFTWARE\CoolSoft");
        keys.Should().Contain(@"HKCU\Software\CoolSoft");
    }

    [Fact]
    public void BuildKeyListForApp_IncludesPublisherNameCombo()
    {
        var app = new DiscoveredApplication
        {
            Name = "Editor",
            Publisher = "MyCorp"
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        keys.Should().Contain(@"HKLM\SOFTWARE\MyCorp\Editor");
        keys.Should().Contain(@"HKCU\Software\MyCorp\Editor");
    }

    [Fact]
    public void BuildKeyListForApp_Includes32BitKeys()
    {
        var app = new DiscoveredApplication
        {
            Name = "OldApp",
            Publisher = "Vendor",
            Is32Bit = true
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        keys.Should().Contain(k => k.Contains("WOW6432Node"));
    }

    [Fact]
    public void BuildKeyListForApp_IncludesAdditionalPaths()
    {
        var app = new DiscoveredApplication
        {
            Name = "CustomApp",
            AdditionalRegistryPaths = [@"HKLM\SOFTWARE\CustomVendor\SpecialKey"]
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        keys.Should().Contain(@"HKLM\SOFTWARE\CustomVendor\SpecialKey");
    }

    [Fact]
    public void BuildKeyListForApp_DeduplicatesKeys()
    {
        var app = new DiscoveredApplication
        {
            Name = "MyApp",
            Publisher = "MyApp" // Same as name — should deduplicate
        };

        var keys = RegistryCaptureService.BuildKeyListForApp(app);

        // HKLM\SOFTWARE\MyApp should appear only once
        keys.Count(k => k.Equals(@"HKLM\SOFTWARE\MyApp", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
    }

    #endregion

    #region IsHardwareKey

    [Theory]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet\Enum\PCI\Device1", true)]
    [InlineData(@"HKLM\SYSTEM\MountedDevices", true)]
    [InlineData(@"HKLM\HARDWARE\Description", true)]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate", true)]
    [InlineData(@"HKLM\SOFTWARE\MyApp\Settings", false)]
    [InlineData(@"HKCU\Software\CoolApp", false)]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MyApp", false)]
    public void IsHardwareKey_FiltersCorrectly(string keyPath, bool expected)
    {
        RegistryCaptureService.IsHardwareKey(keyPath).Should().Be(expected);
    }

    #endregion

    #region RemapRegFilePathsAsync

    [Fact]
    public async Task RemapRegFilePathsAsync_RemapsDoubleBackslashPaths()
    {
        Directory.CreateDirectory(_tempDir);

        var regContent = "Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\TestApp]\n\"Path\"=\"C:\\\\Users\\\\Bill\\\\AppData\\\\Roaming\\\\TestApp\"\n";
        var regFile = Path.Combine(_tempDir, "test.reg");
        await File.WriteAllTextAsync(regFile, regContent);

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = await _service.RemapRegFilePathsAsync(regFile, mappings, CancellationToken.None);

        result.Should().NotBe(regFile); // Should be a temp file
        var content = await File.ReadAllTextAsync(result);
        content.Should().Contain("William");
        content.Should().NotContain("Bill");

        // Clean up
        if (File.Exists(result)) File.Delete(result);
    }

    [Fact]
    public async Task RemapRegFilePathsAsync_ReturnsOriginalIfNoRemappingNeeded()
    {
        Directory.CreateDirectory(_tempDir);

        var regContent = "Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\TestApp]\n\"Key\"=\"Value\"\n";
        var regFile = Path.Combine(_tempDir, "test.reg");
        await File.WriteAllTextAsync(regFile, regContent);

        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\Bill" // Same — no remapping
            }
        };

        var result = await _service.RemapRegFilePathsAsync(regFile, mappings, CancellationToken.None);

        result.Should().Be(regFile); // No temp file created
    }

    #endregion
}
