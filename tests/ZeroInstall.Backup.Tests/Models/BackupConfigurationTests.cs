using System.Text.Json;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests.Models;

public class BackupConfigurationTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var config = new BackupConfiguration();

        config.CustomerId.Should().BeEmpty();
        config.FileBackupCron.Should().Be("0 2 * * *");
        config.FullImageCron.Should().Be("0 3 1 * *");
        config.FullImageVolume.Should().Be("C");
        config.CompressBeforeUpload.Should().BeTrue();
        config.EnableFullImageBackup.Should().BeFalse();
        config.EncryptionPassphrase.Should().BeNull();
        config.QuotaBytes.Should().Be(0);
        config.ConfigSyncIntervalMinutes.Should().Be(15);
        config.Retention.Should().NotBeNull();
        config.BackupPaths.Should().BeEmpty();
        config.ExcludePatterns.Should().BeEmpty();
    }

    [Fact]
    public void GetNasCustomerPath_ReturnsCorrectPath()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        config.GetNasCustomerPath().Should().Be("/backups/customers/cust-001");
    }

    [Fact]
    public void GetNasCustomerPath_TrimsTrailingSlash()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-002",
            NasConnection = { RemoteBasePath = "/backups/" }
        };

        config.GetNasCustomerPath().Should().Be("/backups/customers/cust-002");
    }

    [Fact]
    public void GetNasFileBackupPath_ReturnsCorrectPath()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        config.GetNasFileBackupPath().Should().Be("/backups/customers/cust-001/data/file-backups");
    }

    [Fact]
    public void GetNasFullImagePath_ReturnsCorrectPath()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        config.GetNasFullImagePath().Should().Be("/backups/customers/cust-001/data/full-images");
    }

    [Fact]
    public void GetNasConfigPath_ReturnsCorrectPath()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        config.GetNasConfigPath().Should().Be("/backups/customers/cust-001/config");
    }

    [Fact]
    public void GetNasStatusPath_ReturnsCorrectPath()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        config.GetNasStatusPath().Should().Be("/backups/customers/cust-001/status");
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-json",
            DisplayName = "Test Customer",
            FileBackupCron = "0 3 * * *",
            EnableFullImageBackup = true,
            EncryptionPassphrase = "secret123",
            QuotaBytes = 10_000_000_000L,
            BackupPaths = { @"C:\Users\Test\Documents", @"C:\Users\Test\Desktop" },
            ExcludePatterns = { "*.tmp", "Thumbs.db" },
            Retention = new RetentionPolicy { KeepLastFileBackups = 10, KeepLastFullImages = 2 }
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<BackupConfiguration>(json)!;

        deserialized.CustomerId.Should().Be("cust-json");
        deserialized.DisplayName.Should().Be("Test Customer");
        deserialized.EnableFullImageBackup.Should().BeTrue();
        deserialized.EncryptionPassphrase.Should().Be("secret123");
        deserialized.QuotaBytes.Should().Be(10_000_000_000L);
        deserialized.BackupPaths.Should().HaveCount(2);
        deserialized.ExcludePatterns.Should().HaveCount(2);
        deserialized.Retention.KeepLastFileBackups.Should().Be(10);
        deserialized.Retention.KeepLastFullImages.Should().Be(2);
    }
}
