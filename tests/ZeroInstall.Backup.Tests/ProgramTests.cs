using System.Text.Json;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests;

public class ProgramTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-prog-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task LoadOrCreateConfigAsync_CreatesDefaultWhenMissing()
    {
        var configPath = Path.Combine(_tempDir, "config", "backup-config.json");

        var config = await Program.LoadOrCreateConfigAsync(configPath);

        config.Should().NotBeNull();
        config.CustomerId.Should().NotBeEmpty();
        config.DisplayName.Should().NotBeEmpty();
        config.BackupPaths.Should().HaveCountGreaterOrEqualTo(1);
        config.ExcludePatterns.Should().Contain("*.tmp");

        File.Exists(configPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadOrCreateConfigAsync_LoadsExistingConfig()
    {
        var configPath = Path.Combine(_tempDir, "existing-config.json");
        var original = new BackupConfiguration
        {
            CustomerId = "existing-cust",
            DisplayName = "Existing PC",
            FileBackupCron = "0 4 * * *"
        };

        await using (var stream = File.Create(configPath))
        {
            await JsonSerializer.SerializeAsync(stream, original);
        }

        var loaded = await Program.LoadOrCreateConfigAsync(configPath);

        loaded.CustomerId.Should().Be("existing-cust");
        loaded.DisplayName.Should().Be("Existing PC");
        loaded.FileBackupCron.Should().Be("0 4 * * *");
    }

    [Fact]
    public async Task Main_Help_ReturnsZero()
    {
        var result = await Program.Main(new[] { "--help" });

        result.Should().Be(0);
    }

    [Fact]
    public async Task Main_Status_ShowsConfig()
    {
        var configPath = Path.Combine(_tempDir, "status-config.json");
        var config = new BackupConfiguration
        {
            CustomerId = "status-test",
            DisplayName = "Status Test PC",
            NasConnection = { Host = "nas.local", RemoteBasePath = "/backups" }
        };

        await using (var stream = File.Create(configPath))
        {
            await JsonSerializer.SerializeAsync(stream, config);
        }

        var result = await Program.Main(new[] { "status", "--config", configPath });

        result.Should().Be(0);
    }
}
