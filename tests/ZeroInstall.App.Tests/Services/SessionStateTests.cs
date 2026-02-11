using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Tests.Services;

public class SessionStateTests
{
    private readonly SessionState _sut = new();

    [Fact]
    public void DefaultValues_ShouldBeEmpty()
    {
        _sut.Role.Should().Be(MachineRole.Source);
        _sut.SelectedItems.Should().BeEmpty();
        _sut.UserMappings.Should().BeEmpty();
        _sut.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.OutputPath.Should().BeEmpty();
        _sut.InputPath.Should().BeEmpty();
        _sut.CurrentJob.Should().BeNull();
    }

    [Fact]
    public void SetProperties_ShouldRetainValues()
    {
        var item = new MigrationItem { DisplayName = "Chrome" };
        var mapping = new UserMapping { DestinationUsername = "Bill" };
        var job = new MigrationJob { SourceHostname = "PC1" };

        _sut.Role = MachineRole.Destination;
        _sut.SelectedItems = [item];
        _sut.UserMappings = [mapping];
        _sut.TransportMethod = TransportMethod.NetworkShare;
        _sut.OutputPath = @"E:\capture";
        _sut.InputPath = @"E:\restore";
        _sut.CurrentJob = job;

        _sut.Role.Should().Be(MachineRole.Destination);
        _sut.SelectedItems.Should().ContainSingle().Which.DisplayName.Should().Be("Chrome");
        _sut.UserMappings.Should().ContainSingle().Which.DestinationUsername.Should().Be("Bill");
        _sut.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        _sut.OutputPath.Should().Be(@"E:\capture");
        _sut.InputPath.Should().Be(@"E:\restore");
        _sut.CurrentJob.Should().Be(job);
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        _sut.Role = MachineRole.Destination;
        _sut.SelectedItems = [new MigrationItem()];
        _sut.UserMappings = [new UserMapping()];
        _sut.TransportMethod = TransportMethod.DirectWiFi;
        _sut.OutputPath = @"E:\capture";
        _sut.InputPath = @"E:\restore";
        _sut.CurrentJob = new MigrationJob();
        _sut.NetworkSharePath = @"\\nas\share";
        _sut.NetworkShareUsername = "admin";
        _sut.NetworkSharePassword = "pass";
        _sut.DirectWiFiPort = 12345;
        _sut.DirectWiFiSharedKey = "secret";

        _sut.Reset();

        _sut.Role.Should().Be(MachineRole.Source);
        _sut.SelectedItems.Should().BeEmpty();
        _sut.UserMappings.Should().BeEmpty();
        _sut.TransportMethod.Should().Be(TransportMethod.ExternalStorage);
        _sut.OutputPath.Should().BeEmpty();
        _sut.InputPath.Should().BeEmpty();
        _sut.CurrentJob.Should().BeNull();
        _sut.NetworkSharePath.Should().BeEmpty();
        _sut.NetworkShareUsername.Should().BeEmpty();
        _sut.NetworkSharePassword.Should().BeEmpty();
        _sut.DirectWiFiPort.Should().Be(19850);
        _sut.DirectWiFiSharedKey.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsISessionState()
    {
        _sut.Should().BeAssignableTo<ISessionState>();
    }

    [Fact]
    public void Reset_CalledTwice_ShouldBeIdempotent()
    {
        _sut.Role = MachineRole.Destination;
        _sut.OutputPath = @"E:\test";

        _sut.Reset();
        _sut.Reset();

        _sut.Role.Should().Be(MachineRole.Source);
        _sut.OutputPath.Should().BeEmpty();
    }

    [Fact]
    public void TransportConfig_DefaultValues()
    {
        _sut.NetworkSharePath.Should().BeEmpty();
        _sut.NetworkShareUsername.Should().BeEmpty();
        _sut.NetworkSharePassword.Should().BeEmpty();
        _sut.DirectWiFiPort.Should().Be(19850);
        _sut.DirectWiFiSharedKey.Should().BeEmpty();
    }

    [Fact]
    public void TransportConfig_SetProperties_ShouldRetainValues()
    {
        _sut.NetworkSharePath = @"\\nas\data";
        _sut.NetworkShareUsername = "user1";
        _sut.NetworkSharePassword = "p@ss";
        _sut.DirectWiFiPort = 9999;
        _sut.DirectWiFiSharedKey = "mykey";

        _sut.NetworkSharePath.Should().Be(@"\\nas\data");
        _sut.NetworkShareUsername.Should().Be("user1");
        _sut.NetworkSharePassword.Should().Be("p@ss");
        _sut.DirectWiFiPort.Should().Be(9999);
        _sut.DirectWiFiSharedKey.Should().Be("mykey");
    }

    [Fact]
    public void Reset_ClearsTransportConfig()
    {
        _sut.NetworkSharePath = @"\\nas\share";
        _sut.DirectWiFiPort = 12345;

        _sut.Reset();

        _sut.NetworkSharePath.Should().BeEmpty();
        _sut.DirectWiFiPort.Should().Be(19850);
    }

    [Fact]
    public void Reset_ClearsNetworkCredentials()
    {
        _sut.NetworkShareUsername = "admin";
        _sut.NetworkSharePassword = "secret";

        _sut.Reset();

        _sut.NetworkShareUsername.Should().BeEmpty();
        _sut.NetworkSharePassword.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsDirectWiFiSharedKey()
    {
        _sut.DirectWiFiSharedKey = "key123";

        _sut.Reset();

        _sut.DirectWiFiSharedKey.Should().BeEmpty();
    }

    [Fact]
    public void SftpConfig_DefaultValues()
    {
        _sut.SftpHost.Should().BeEmpty();
        _sut.SftpPort.Should().Be(22);
        _sut.SftpUsername.Should().BeEmpty();
        _sut.SftpPassword.Should().BeEmpty();
        _sut.SftpPrivateKeyPath.Should().BeEmpty();
        _sut.SftpPrivateKeyPassphrase.Should().BeEmpty();
        _sut.SftpRemoteBasePath.Should().Be("/backups/zim");
        _sut.SftpEncryptionPassphrase.Should().BeEmpty();
        _sut.SftpCompressBeforeUpload.Should().BeTrue();
    }

    [Fact]
    public void SftpConfig_SetProperties_ShouldRetainValues()
    {
        _sut.SftpHost = "nas.test.com";
        _sut.SftpPort = 2222;
        _sut.SftpUsername = "sftpuser";
        _sut.SftpPassword = "sftppass";
        _sut.SftpPrivateKeyPath = @"C:\keys\id_rsa";
        _sut.SftpPrivateKeyPassphrase = "keypass";
        _sut.SftpRemoteBasePath = "/data";
        _sut.SftpEncryptionPassphrase = "aes";
        _sut.SftpCompressBeforeUpload = false;

        _sut.SftpHost.Should().Be("nas.test.com");
        _sut.SftpPort.Should().Be(2222);
        _sut.SftpUsername.Should().Be("sftpuser");
        _sut.SftpPassword.Should().Be("sftppass");
        _sut.SftpPrivateKeyPath.Should().Be(@"C:\keys\id_rsa");
        _sut.SftpPrivateKeyPassphrase.Should().Be("keypass");
        _sut.SftpRemoteBasePath.Should().Be("/data");
        _sut.SftpEncryptionPassphrase.Should().Be("aes");
        _sut.SftpCompressBeforeUpload.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsSftpConfig()
    {
        _sut.SftpHost = "nas.test.com";
        _sut.SftpPort = 2222;
        _sut.SftpUsername = "user";
        _sut.SftpPassword = "pass";
        _sut.SftpPrivateKeyPath = @"C:\key";
        _sut.SftpPrivateKeyPassphrase = "kp";
        _sut.SftpRemoteBasePath = "/custom";
        _sut.SftpEncryptionPassphrase = "enc";
        _sut.SftpCompressBeforeUpload = false;

        _sut.Reset();

        _sut.SftpHost.Should().BeEmpty();
        _sut.SftpPort.Should().Be(22);
        _sut.SftpUsername.Should().BeEmpty();
        _sut.SftpPassword.Should().BeEmpty();
        _sut.SftpPrivateKeyPath.Should().BeEmpty();
        _sut.SftpPrivateKeyPassphrase.Should().BeEmpty();
        _sut.SftpRemoteBasePath.Should().Be("/backups/zim");
        _sut.SftpEncryptionPassphrase.Should().BeEmpty();
        _sut.SftpCompressBeforeUpload.Should().BeTrue();
    }

    [Fact]
    public void Reset_SftpConfig_IsIdempotent()
    {
        _sut.SftpHost = "test.com";
        _sut.SftpEncryptionPassphrase = "secret";

        _sut.Reset();
        _sut.Reset();

        _sut.SftpHost.Should().BeEmpty();
        _sut.SftpEncryptionPassphrase.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsSftpButNotDefaults()
    {
        _sut.Reset();

        _sut.SftpPort.Should().Be(22);
        _sut.SftpRemoteBasePath.Should().Be("/backups/zim");
        _sut.SftpCompressBeforeUpload.Should().BeTrue();
    }

    [Fact]
    public void BluetoothConfig_DefaultValues()
    {
        _sut.BluetoothDeviceName.Should().BeEmpty();
        _sut.BluetoothDeviceAddress.Should().Be(0UL);
        _sut.BluetoothIsServer.Should().BeFalse();
    }

    [Fact]
    public void BluetoothConfig_SetProperties_ShouldRetainValues()
    {
        _sut.BluetoothDeviceName = "TestPC";
        _sut.BluetoothDeviceAddress = 0xAABBCCDDEEFF;
        _sut.BluetoothIsServer = true;

        _sut.BluetoothDeviceName.Should().Be("TestPC");
        _sut.BluetoothDeviceAddress.Should().Be(0xAABBCCDDEEFF);
        _sut.BluetoothIsServer.Should().BeTrue();
    }

    [Fact]
    public void Reset_ClearsBluetoothConfig()
    {
        _sut.BluetoothDeviceName = "RemotePC";
        _sut.BluetoothDeviceAddress = 12345UL;
        _sut.BluetoothIsServer = true;

        _sut.Reset();

        _sut.BluetoothDeviceName.Should().BeEmpty();
        _sut.BluetoothDeviceAddress.Should().Be(0UL);
        _sut.BluetoothIsServer.Should().BeFalse();
    }

    [Fact]
    public void Reset_BluetoothConfig_IsIdempotent()
    {
        _sut.BluetoothDeviceName = "PC1";

        _sut.Reset();
        _sut.Reset();

        _sut.BluetoothDeviceName.Should().BeEmpty();
    }
}
