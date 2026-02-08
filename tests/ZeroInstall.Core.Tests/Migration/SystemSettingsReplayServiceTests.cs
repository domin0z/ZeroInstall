using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class SystemSettingsReplayServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly SystemSettingsReplayService _service;
    private readonly string _tempDir;

    public SystemSettingsReplayServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-ssr-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new SystemSettingsReplayService(
            _processRunner, _registry, _fileSystem,
            NullLogger<SystemSettingsReplayService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static MigrationItem CreateSettingItem(
        string name, SystemSettingCategory category, string? data = null)
    {
        return new MigrationItem
        {
            DisplayName = name,
            ItemType = MigrationItemType.SystemSetting,
            IsSelected = true,
            SourceData = new SystemSetting
            {
                Name = name,
                Category = category,
                Data = data
            }
        };
    }

    #region CaptureAsync

    [Fact]
    public async Task CaptureAsync_CreatesManifest()
    {
        _processRunner.RunAsync("netsh", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var items = new List<MigrationItem>
        {
            CreateSettingItem("HomeWifi", SystemSettingCategory.WifiProfile)
        };

        var outputDir = Path.Combine(_tempDir, "capture");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "system-settings-manifest.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_CapturesWifiProfile()
    {
        _processRunner.RunAsync("netsh", Arg.Is<string>(s => s.Contains("export")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var items = new List<MigrationItem>
        {
            CreateSettingItem("MyWiFi", SystemSettingCategory.WifiProfile)
        };

        var outputDir = Path.Combine(_tempDir, "wifi-capture");
        await _service.CaptureAsync(items, outputDir);

        await _processRunner.Received().RunAsync("netsh",
            Arg.Is<string>(s => s.Contains("MyWiFi") && s.Contains("export")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureAsync_CapturesScheduledTask()
    {
        _processRunner.RunAsync("schtasks", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "<Task>xml</Task>" });

        var items = new List<MigrationItem>
        {
            CreateSettingItem("BackupJob", SystemSettingCategory.ScheduledTask)
        };

        var outputDir = Path.Combine(_tempDir, "task-capture");
        await _service.CaptureAsync(items, outputDir);

        await _processRunner.Received().RunAsync("schtasks",
            Arg.Is<string>(s => s.Contains("BackupJob") && s.Contains("/query")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureAsync_CapturesDefaultApps()
    {
        _processRunner.RunAsync("dism", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var items = new List<MigrationItem>
        {
            CreateSettingItem("Default Apps", SystemSettingCategory.DefaultAppAssociation)
        };

        var outputDir = Path.Combine(_tempDir, "dism-capture");
        await _service.CaptureAsync(items, outputDir);

        await _processRunner.Received().RunAsync("dism",
            Arg.Is<string>(s => s.Contains("export-defaultappassociations")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureAsync_SetsWarningForCredentials()
    {
        var item = CreateSettingItem("WindowsVault", SystemSettingCategory.Credential);

        var outputDir = Path.Combine(_tempDir, "cred-capture");
        await _service.CaptureAsync(new[] { item }, outputDir);

        item.Status.Should().Be(MigrationItemStatus.Completed);

        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "system-settings-manifest.json"));
        json.Should().Contain("Warning");
        json.Should().Contain("re-enter manually");
    }

    [Fact]
    public async Task CaptureAsync_SetsWarningForCertificates()
    {
        var item = CreateSettingItem("PersonalCerts", SystemSettingCategory.Certificate);

        var outputDir = Path.Combine(_tempDir, "cert-capture");
        await _service.CaptureAsync(new[] { item }, outputDir);

        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "system-settings-manifest.json"));
        json.Should().Contain("Warning");
        json.Should().Contain("manual export");
    }

    [Fact]
    public async Task CaptureAsync_SkipsNonSystemSettingItems()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "App",
                ItemType = MigrationItemType.Application,
                IsSelected = true
            }
        };

        var outputDir = Path.Combine(_tempDir, "skip-test");
        await _service.CaptureAsync(items, outputDir);

        File.Exists(Path.Combine(outputDir, "system-settings-manifest.json")).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureAsync_SetsItemStatusOnSuccess()
    {
        var item = CreateSettingItem("TestDrive", SystemSettingCategory.MappedDrive, "Z:|\\\\server\\share");

        var outputDir = Path.Combine(_tempDir, "status-test");
        await _service.CaptureAsync(new[] { item }, outputDir);

        item.Status.Should().Be(MigrationItemStatus.Completed);
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_RestoresWifiProfile()
    {
        var captureDir = Path.Combine(_tempDir, "wifi-restore");
        var wifiDir = Path.Combine(captureDir, "wifi");
        Directory.CreateDirectory(wifiDir);
        await File.WriteAllTextAsync(Path.Combine(wifiDir, "HomeNet.xml"), "<WifiProfile/>");

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "HomeNet",
                    Category = SystemSettingCategory.WifiProfile,
                    CaptureFileName = "wifi/HomeNet.xml",
                    Status = CapturedSettingStatus.Captured
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        _processRunner.RunAsync("netsh", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        await _processRunner.Received().RunAsync("netsh",
            Arg.Is<string>(s => s.Contains("wlan add profile")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_RestoresMappedDrive()
    {
        var captureDir = Path.Combine(_tempDir, "drive-restore");
        Directory.CreateDirectory(captureDir);

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "Z: Drive",
                    Category = SystemSettingCategory.MappedDrive,
                    Data = @"Z:|\\server\share",
                    Status = CapturedSettingStatus.Captured
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        _processRunner.RunAsync("net", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        await _processRunner.Received().RunAsync("net",
            Arg.Is<string>(s => s.Contains("use Z:") && s.Contains("persistent")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_RestoresScheduledTask()
    {
        var captureDir = Path.Combine(_tempDir, "task-restore");
        var tasksDir = Path.Combine(captureDir, "tasks");
        Directory.CreateDirectory(tasksDir);
        await File.WriteAllTextAsync(Path.Combine(tasksDir, "BackupJob.xml"), "<Task/>");

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "BackupJob",
                    Category = SystemSettingCategory.ScheduledTask,
                    CaptureFileName = "tasks/BackupJob.xml",
                    Status = CapturedSettingStatus.Captured
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        _processRunner.RunAsync("schtasks", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        await _processRunner.Received().RunAsync("schtasks",
            Arg.Is<string>(s => s.Contains("/create") && s.Contains("BackupJob")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_RestoresEnvironmentVariable()
    {
        var captureDir = Path.Combine(_tempDir, "env-restore");
        Directory.CreateDirectory(captureDir);

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "JAVA_HOME",
                    Category = SystemSettingCategory.EnvironmentVariable,
                    Data = @"JAVA_HOME=C:\Program Files\Java\jdk-17",
                    Status = CapturedSettingStatus.Captured
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        _processRunner.RunAsync("setx", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        await _processRunner.Received().RunAsync("setx",
            Arg.Is<string>(s => s.Contains("JAVA_HOME")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_RestoresNetworkPrinter()
    {
        var captureDir = Path.Combine(_tempDir, "printer-restore");
        Directory.CreateDirectory(captureDir);

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "OfficePrinter",
                    Category = SystemSettingCategory.Printer,
                    Data = @"\\printserver\HP-LaserJet",
                    Status = CapturedSettingStatus.Captured
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        await _processRunner.Received().RunAsync("powershell",
            Arg.Is<string>(s => s.Contains("Add-Printer")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_SkipsWarningEntries()
    {
        var captureDir = Path.Combine(_tempDir, "skip-warning");
        Directory.CreateDirectory(captureDir);

        var manifest = new SystemSettingsCaptureManifest
        {
            Settings =
            [
                new CapturedSettingEntry
                {
                    Name = "Credential",
                    Category = SystemSettingCategory.Credential,
                    Status = CapturedSettingStatus.Warning,
                    StatusMessage = "Re-enter manually"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(captureDir, "system-settings-manifest.json"),
            System.Text.Json.JsonSerializer.Serialize(manifest));

        await _service.RestoreAsync(captureDir, new List<UserMapping>());

        // Should not call any process runner for warning entries
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_HandlesNoManifest()
    {
        var captureDir = Path.Combine(_tempDir, "no-manifest");
        Directory.CreateDirectory(captureDir);

        // Should not throw
        await _service.RestoreAsync(captureDir, new List<UserMapping>());
    }

    #endregion

    #region RemapEnvironmentValue

    [Fact]
    public void RemapEnvironmentValue_ReplacesPaths()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = SystemSettingsReplayService.RemapEnvironmentValue(
            @"C:\Users\Bill\AppData\Local\Tools", mappings);

        result.Should().Be(@"C:\Users\William\AppData\Local\Tools");
    }

    [Fact]
    public void RemapEnvironmentValue_CaseInsensitive()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = SystemSettingsReplayService.RemapEnvironmentValue(
            @"c:\users\bill\tools", mappings);

        result.Should().Be(@"C:\Users\William\tools");
    }

    [Fact]
    public void RemapEnvironmentValue_NoMatchReturnsOriginal()
    {
        var mappings = new List<UserMapping>
        {
            new()
            {
                SourceUser = new UserProfile { ProfilePath = @"C:\Users\Bill" },
                DestinationProfilePath = @"C:\Users\William"
            }
        };

        var result = SystemSettingsReplayService.RemapEnvironmentValue(
            @"C:\Program Files\Java", mappings);

        result.Should().Be(@"C:\Program Files\Java");
    }

    #endregion
}
