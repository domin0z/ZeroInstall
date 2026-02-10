using System.Text.Json;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class SftpTransportConfigurationTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new SftpTransportConfiguration();

        config.Host.Should().BeEmpty();
        config.Port.Should().Be(22);
        config.Username.Should().BeEmpty();
        config.Password.Should().BeNull();
        config.PrivateKeyPath.Should().BeNull();
        config.PrivateKeyPassphrase.Should().BeNull();
        config.RemoteBasePath.Should().Be("/backups/zim");
        config.EncryptionPassphrase.Should().BeNull();
        config.CompressBeforeUpload.Should().BeTrue();
    }

    [Fact]
    public void AllProperties_AreSettable()
    {
        var config = new SftpTransportConfiguration
        {
            Host = "nas.example.com",
            Port = 2222,
            Username = "admin",
            Password = "secret",
            PrivateKeyPath = "/home/user/.ssh/id_rsa",
            PrivateKeyPassphrase = "keypass",
            RemoteBasePath = "/data/backups",
            EncryptionPassphrase = "encrypt-me",
            CompressBeforeUpload = false
        };

        config.Host.Should().Be("nas.example.com");
        config.Port.Should().Be(2222);
        config.Username.Should().Be("admin");
        config.Password.Should().Be("secret");
        config.PrivateKeyPath.Should().Be("/home/user/.ssh/id_rsa");
        config.PrivateKeyPassphrase.Should().Be("keypass");
        config.RemoteBasePath.Should().Be("/data/backups");
        config.EncryptionPassphrase.Should().Be("encrypt-me");
        config.CompressBeforeUpload.Should().BeFalse();
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new SftpTransportConfiguration
        {
            Host = "sftp.test.com",
            Port = 3333,
            Username = "user1",
            Password = "pass1",
            PrivateKeyPath = "/keys/key.pem",
            PrivateKeyPassphrase = "pem-pass",
            RemoteBasePath = "/custom/path",
            EncryptionPassphrase = "aes-key",
            CompressBeforeUpload = false
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SftpTransportConfiguration>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Host.Should().Be(original.Host);
        deserialized.Port.Should().Be(original.Port);
        deserialized.Username.Should().Be(original.Username);
        deserialized.Password.Should().Be(original.Password);
        deserialized.PrivateKeyPath.Should().Be(original.PrivateKeyPath);
        deserialized.PrivateKeyPassphrase.Should().Be(original.PrivateKeyPassphrase);
        deserialized.RemoteBasePath.Should().Be(original.RemoteBasePath);
        deserialized.EncryptionPassphrase.Should().Be(original.EncryptionPassphrase);
        deserialized.CompressBeforeUpload.Should().Be(original.CompressBeforeUpload);
    }
}
