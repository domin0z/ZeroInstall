using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.App.Tests.ViewModels;

public class CaptureConfigViewModelTests
{
    private readonly ISessionState _session = new SessionState();
    private readonly INavigationService _navService = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly CaptureConfigViewModel _sut;

    public CaptureConfigViewModelTests()
    {
        _sut = new CaptureConfigViewModel(_session, _navService, _dialogService);
    }

    [Fact]
    public void Title_ShouldBeConfigure()
    {
        _sut.Title.Should().Be("Configure");
    }

    [Fact]
    public async Task OnNavigatedTo_ComputesSummaryStats()
    {
        _session.SelectedItems =
        [
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 100 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 200 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.RegistryFile, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 50 },
            new MigrationItem { IsSelected = true, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.UserProfile, EstimatedSizeBytes = 1024 * 1024 * 500 },
            new MigrationItem { IsSelected = false, RecommendedTier = MigrationTier.Package, ItemType = MigrationItemType.Application, EstimatedSizeBytes = 1024 * 1024 * 999 },
        ];

        await _sut.OnNavigatedTo();

        _sut.PackageItemCount.Should().Be(3); // 2 apps + 1 UserProfile with Package tier
        _sut.RegFileItemCount.Should().Be(1);
        _sut.ProfileItemCount.Should().Be(1);
        _sut.TotalSizeFormatted.Should().NotBe("0 B");
    }

    [Fact]
    public void CanStartCapture_IsFalse_WhenOutputPathEmpty()
    {
        _sut.OutputPath = "";
        _sut.CanStartCapture().Should().BeFalse();
    }

    [Fact]
    public void CanStartCapture_IsTrue_WhenOutputPathSet()
    {
        _sut.OutputPath = @"E:\capture";
        _sut.CanStartCapture().Should().BeTrue();
    }

    [Fact]
    public void StartCapture_SavesToSessionAndNavigates()
    {
        _sut.OutputPath = @"E:\capture";
        _sut.SelectedTransport = TransportMethod.NetworkShare;

        _sut.StartCaptureCommand.Execute(null);

        _session.OutputPath.Should().Be(@"E:\capture");
        _session.TransportMethod.Should().Be(TransportMethod.NetworkShare);
        _navService.Received(1).NavigateTo<MigrationProgressViewModel>();
    }

    [Fact]
    public async Task OnNavigatedTo_WithNoItems_ShowsZeroCounts()
    {
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.PackageItemCount.Should().Be(0);
        _sut.RegFileItemCount.Should().Be(0);
        _sut.ProfileItemCount.Should().Be(0);
        _sut.TotalSizeFormatted.Should().Be("0 B");
    }

    [Fact]
    public async Task BrowseOutput_WhenDialogReturnsPath_SetsOutputPath()
    {
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(@"E:\selected");

        await _sut.BrowseOutputCommand.ExecuteAsync(null);

        _sut.OutputPath.Should().Be(@"E:\selected");
    }

    [Fact]
    public async Task BrowseOutput_WhenDialogCancelled_DoesNotChangeOutputPath()
    {
        _sut.OutputPath = @"E:\original";
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((string?)null);

        await _sut.BrowseOutputCommand.ExecuteAsync(null);

        _sut.OutputPath.Should().Be(@"E:\original");
    }

    [Fact]
    public void StartCapture_SavesTransportConfigToSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.SelectedTransport = TransportMethod.NetworkShare;
        _sut.NetworkSharePath = @"\\nas\share";
        _sut.NetworkShareUsername = "admin";
        _sut.NetworkSharePassword = "pass";

        _sut.StartCaptureCommand.Execute(null);

        _session.NetworkSharePath.Should().Be(@"\\nas\share");
        _session.NetworkShareUsername.Should().Be("admin");
        _session.NetworkSharePassword.Should().Be("pass");
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresTransportConfigFromSession()
    {
        _session.NetworkSharePath = @"\\restored\path";
        _session.DirectWiFiPort = 12345;
        _session.DirectWiFiSharedKey = "key123";

        await _sut.OnNavigatedTo();

        _sut.NetworkSharePath.Should().Be(@"\\restored\path");
        _sut.DirectWiFiPort.Should().Be(12345);
        _sut.DirectWiFiSharedKey.Should().Be("key123");
    }

    [Fact]
    public void StartCapture_SavesDirectWiFiConfigToSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.SelectedTransport = TransportMethod.DirectWiFi;
        _sut.DirectWiFiPort = 9999;
        _sut.DirectWiFiSharedKey = "secret";

        _sut.StartCaptureCommand.Execute(null);

        _session.DirectWiFiPort.Should().Be(9999);
        _session.DirectWiFiSharedKey.Should().Be("secret");
    }

    [Fact]
    public void TransportMethods_IncludesSftp()
    {
        _sut.TransportMethods.Should().Contain(TransportMethod.Sftp);
    }

    [Fact]
    public void TransportMethods_HasFiveEntries_Legacy()
    {
        // Updated from 4 to 5 with Bluetooth transport
        _sut.TransportMethods.Should().HaveCount(5);
    }

    [Fact]
    public void StartCapture_SavesSftpConfigToSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.SelectedTransport = TransportMethod.Sftp;
        _sut.SftpHost = "nas.example.com";
        _sut.SftpPort = 2222;
        _sut.SftpUsername = "admin";
        _sut.SftpPassword = "pass123";
        _sut.SftpPrivateKeyPath = @"C:\keys\id_rsa";
        _sut.SftpPrivateKeyPassphrase = "keypass";
        _sut.SftpRemoteBasePath = "/data/backup";
        _sut.SftpEncryptionPassphrase = "aes-secret";
        _sut.SftpCompressBeforeUpload = false;

        _sut.StartCaptureCommand.Execute(null);

        _session.SftpHost.Should().Be("nas.example.com");
        _session.SftpPort.Should().Be(2222);
        _session.SftpUsername.Should().Be("admin");
        _session.SftpPassword.Should().Be("pass123");
        _session.SftpPrivateKeyPath.Should().Be(@"C:\keys\id_rsa");
        _session.SftpPrivateKeyPassphrase.Should().Be("keypass");
        _session.SftpRemoteBasePath.Should().Be("/data/backup");
        _session.SftpEncryptionPassphrase.Should().Be("aes-secret");
        _session.SftpCompressBeforeUpload.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresSftpConfigFromSession()
    {
        _session.SftpHost = "sftp.test.com";
        _session.SftpPort = 3333;
        _session.SftpUsername = "testuser";
        _session.SftpPassword = "testpass";
        _session.SftpPrivateKeyPath = @"C:\test\key";
        _session.SftpPrivateKeyPassphrase = "kp";
        _session.SftpRemoteBasePath = "/custom";
        _session.SftpEncryptionPassphrase = "enc";
        _session.SftpCompressBeforeUpload = false;

        await _sut.OnNavigatedTo();

        _sut.SftpHost.Should().Be("sftp.test.com");
        _sut.SftpPort.Should().Be(3333);
        _sut.SftpUsername.Should().Be("testuser");
        _sut.SftpPassword.Should().Be("testpass");
        _sut.SftpPrivateKeyPath.Should().Be(@"C:\test\key");
        _sut.SftpPrivateKeyPassphrase.Should().Be("kp");
        _sut.SftpRemoteBasePath.Should().Be("/custom");
        _sut.SftpEncryptionPassphrase.Should().Be("enc");
        _sut.SftpCompressBeforeUpload.Should().BeFalse();
    }

    [Fact]
    public void SftpDefaultValues_AreCorrect()
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
    public void SftpIsConnected_DefaultsFalse()
    {
        _sut.SftpIsConnected.Should().BeFalse();
    }

    [Fact]
    public void SftpBrowseItems_DefaultsEmpty()
    {
        _sut.SftpBrowseItems.Should().BeEmpty();
    }

    [Fact]
    public void SftpConnectionStatus_DefaultsEmpty()
    {
        _sut.SftpConnectionStatus.Should().BeEmpty();
    }

    [Fact]
    public async Task SftpBrowseKeyFile_WhenDialogReturnsPath_SetsKeyPath()
    {
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(@"C:\keys\my_key");

        await _sut.SftpBrowseKeyFileCommand.ExecuteAsync(null);

        _sut.SftpPrivateKeyPath.Should().Be(@"C:\keys\my_key");
    }

    [Fact]
    public async Task SftpBrowseKeyFile_WhenDialogCancelled_DoesNotChangePath()
    {
        _sut.SftpPrivateKeyPath = @"C:\existing\key";
        _dialogService.BrowseFolderAsync(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((string?)null);

        await _sut.SftpBrowseKeyFileCommand.ExecuteAsync(null);

        _sut.SftpPrivateKeyPath.Should().Be(@"C:\existing\key");
    }

    [Fact]
    public void StartCapture_WithSftp_SetsTransportMethodOnSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.SelectedTransport = TransportMethod.Sftp;

        _sut.StartCaptureCommand.Execute(null);

        _session.TransportMethod.Should().Be(TransportMethod.Sftp);
    }

    #region BitLocker Warning

    [Fact]
    public async Task OnNavigatedTo_BitLockerLocked_ShowsWarning()
    {
        var bitLocker = Substitute.For<IBitLockerService>();
        bitLocker.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.Locked,
                LockStatus = "Locked"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, bitLocker);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowBitLockerWarning.Should().BeTrue();
        vm.BitLockerWarning.Should().Contain("LOCKED");
    }

    [Fact]
    public async Task OnNavigatedTo_BitLockerUnlocked_ShowsNotice()
    {
        var bitLocker = Substitute.For<IBitLockerService>();
        bitLocker.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.Unlocked,
                LockStatus = "Unlocked"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, bitLocker);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowBitLockerWarning.Should().BeTrue();
        vm.BitLockerWarning.Should().Contain("encrypted");
    }

    [Fact]
    public async Task OnNavigatedTo_BitLockerNotProtected_NoWarning()
    {
        var bitLocker = Substitute.For<IBitLockerService>();
        bitLocker.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BitLockerStatus
            {
                VolumePath = "C:",
                ProtectionStatus = BitLockerProtectionStatus.NotProtected
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, bitLocker);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowBitLockerWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_NullBitLockerService_NoWarning()
    {
        // Default SUT has no BitLocker service
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.ShowBitLockerWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_BitLockerServiceThrows_NoWarning()
    {
        var bitLocker = Substitute.For<IBitLockerService>();
        bitLocker.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<BitLockerStatus>(_ => throw new InvalidOperationException("error"));

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, bitLocker);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowBitLockerWarning.Should().BeFalse();
    }

    #endregion

    #region Bluetooth

    [Fact]
    public void TransportMethods_IncludesBluetooth()
    {
        _sut.TransportMethods.Should().Contain(TransportMethod.Bluetooth);
    }

    [Fact]
    public void TransportMethods_HasFiveEntries()
    {
        _sut.TransportMethods.Should().HaveCount(5);
    }

    [Fact]
    public void BluetoothDefaultValues_AreCorrect()
    {
        _sut.BluetoothDeviceName.Should().BeEmpty();
        _sut.BluetoothDeviceAddress.Should().Be(0UL);
        _sut.BluetoothIsServer.Should().BeFalse();
        _sut.BluetoothIsScanning.Should().BeFalse();
        _sut.BluetoothIsConnected.Should().BeFalse();
        _sut.BluetoothConnectionStatus.Should().BeEmpty();
        _sut.BluetoothSpeedWarning.Should().BeEmpty();
    }

    [Fact]
    public void BluetoothDiscoveredDevices_DefaultsEmpty()
    {
        _sut.BluetoothDiscoveredDevices.Should().BeEmpty();
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresBluetoothConfigFromSession()
    {
        _session.BluetoothDeviceName = "TestPC";
        _session.BluetoothDeviceAddress = 0xAABBCCDDEEFF;
        _session.BluetoothIsServer = true;

        await _sut.OnNavigatedTo();

        _sut.BluetoothDeviceName.Should().Be("TestPC");
        _sut.BluetoothDeviceAddress.Should().Be(0xAABBCCDDEEFF);
        _sut.BluetoothIsServer.Should().BeTrue();
    }

    [Fact]
    public void StartCapture_SavesBluetoothConfigToSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.SelectedTransport = TransportMethod.Bluetooth;
        _sut.BluetoothDeviceName = "RemotePC";
        _sut.BluetoothDeviceAddress = 12345UL;
        _sut.BluetoothIsServer = true;

        _sut.StartCaptureCommand.Execute(null);

        _session.BluetoothDeviceName.Should().Be("RemotePC");
        _session.BluetoothDeviceAddress.Should().Be(12345UL);
        _session.BluetoothIsServer.Should().BeTrue();
        _session.TransportMethod.Should().Be(TransportMethod.Bluetooth);
    }

    [Fact]
    public void BluetoothProperties_CanBeSet()
    {
        _sut.BluetoothDeviceName = "Device1";
        _sut.BluetoothDeviceAddress = 42UL;
        _sut.BluetoothIsServer = true;
        _sut.BluetoothIsScanning = true;
        _sut.BluetoothIsConnected = true;
        _sut.BluetoothConnectionStatus = "Connected";
        _sut.BluetoothSpeedWarning = "Slow transfer";

        _sut.BluetoothDeviceName.Should().Be("Device1");
        _sut.BluetoothDeviceAddress.Should().Be(42UL);
        _sut.BluetoothIsServer.Should().BeTrue();
        _sut.BluetoothIsScanning.Should().BeTrue();
        _sut.BluetoothIsConnected.Should().BeTrue();
        _sut.BluetoothConnectionStatus.Should().Be("Connected");
        _sut.BluetoothSpeedWarning.Should().Be("Slow transfer");
    }

    #endregion

    #region Firmware

    [Fact]
    public async Task OnNavigatedTo_FirmwareServiceShowsInfo()
    {
        var firmware = Substitute.For<IFirmwareService>();
        firmware.GetFirmwareInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new FirmwareInfo
            {
                FirmwareType = FirmwareType.Uefi,
                SecureBoot = SecureBootStatus.Enabled,
                TpmPresent = true,
                TpmVersion = "2.0",
                BiosVendor = "AMI",
                BiosVersion = "1.0",
                SystemManufacturer = "Dell",
                SystemModel = "OptiPlex 7090"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, firmwareService: firmware);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowFirmwareInfo.Should().BeTrue();
        vm.FirmwareTypeDisplay.Should().Be("Uefi");
        vm.SecureBootDisplay.Should().Be("Enabled");
        vm.TpmDisplay.Should().Contain("2.0");
        vm.BiosInfoDisplay.Should().Contain("AMI");
        vm.SystemInfoDisplay.Should().Contain("Dell");
    }

    [Fact]
    public async Task OnNavigatedTo_FirmwareServiceShowsWarning()
    {
        var firmware = Substitute.For<IFirmwareService>();
        firmware.GetFirmwareInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new FirmwareInfo { FirmwareType = FirmwareType.Uefi });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, firmwareService: firmware);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowFirmwareWarning.Should().BeTrue();
        vm.FirmwareWarning.Should().Contain("cannot be migrated");
    }

    [Fact]
    public async Task OnNavigatedTo_NullFirmwareService_HidesInfo()
    {
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.ShowFirmwareInfo.Should().BeFalse();
        _sut.ShowFirmwareWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_FirmwareServiceThrows_HidesInfo()
    {
        var firmware = Substitute.For<IFirmwareService>();
        firmware.GetFirmwareInfoAsync(Arg.Any<CancellationToken>())
            .Returns<FirmwareInfo>(_ => throw new InvalidOperationException("error"));

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, firmwareService: firmware);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowFirmwareInfo.Should().BeFalse();
        vm.ShowFirmwareWarning.Should().BeFalse();
    }

    [Fact]
    public void IncludeBcdBackup_DefaultsTrue()
    {
        _sut.IncludeBcdBackup.Should().BeTrue();
    }

    [Fact]
    public void StartCapture_SavesBcdBackupFlagToSession()
    {
        _sut.OutputPath = @"E:\out";
        _sut.IncludeBcdBackup = false;

        _sut.StartCaptureCommand.Execute(null);

        _session.IncludeBcdBackup.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresBcdBackupFlagFromSession()
    {
        _session.IncludeBcdBackup = false;
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.IncludeBcdBackup.Should().BeFalse();
    }

    #endregion

    #region Domain Info

    [Fact]
    public async Task OnNavigatedTo_DomainServiceShowsInfo_ForAD()
    {
        var domainService = Substitute.For<IDomainService>();
        domainService.GetDomainInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new DomainInfo
            {
                JoinType = DomainJoinType.ActiveDirectory,
                DomainOrWorkgroup = "corp.local",
                DomainController = "DC01.corp.local"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, domainService: domainService);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowDomainInfo.Should().BeTrue();
        vm.DomainJoinTypeDisplay.Should().Be("ActiveDirectory");
        vm.DomainInfoDisplay.Should().Be("corp.local");
        vm.DomainControllerDisplay.Should().Be("DC01.corp.local");
    }

    [Fact]
    public async Task OnNavigatedTo_DomainAD_ShowsWarning()
    {
        var domainService = Substitute.For<IDomainService>();
        domainService.GetDomainInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new DomainInfo
            {
                JoinType = DomainJoinType.ActiveDirectory,
                DomainOrWorkgroup = "corp.local"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, domainService: domainService);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowDomainWarning.Should().BeTrue();
        vm.DomainWarning.Should().Contain("cannot be migrated");
    }

    [Fact]
    public async Task OnNavigatedTo_DomainWorkgroup_NoWarning()
    {
        var domainService = Substitute.For<IDomainService>();
        domainService.GetDomainInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new DomainInfo
            {
                JoinType = DomainJoinType.Workgroup,
                DomainOrWorkgroup = "WORKGROUP"
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, domainService: domainService);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowDomainInfo.Should().BeTrue();
        vm.ShowDomainWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_NullDomainService_HidesDomainInfo()
    {
        _session.SelectedItems = [];

        await _sut.OnNavigatedTo();

        _sut.ShowDomainInfo.Should().BeFalse();
        _sut.ShowDomainWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_DomainServiceThrows_HidesDomainInfo()
    {
        var domainService = Substitute.For<IDomainService>();
        domainService.GetDomainInfoAsync(Arg.Any<CancellationToken>())
            .Returns<DomainInfo>(_ => throw new InvalidOperationException("error"));

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, domainService: domainService);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.ShowDomainInfo.Should().BeFalse();
        vm.ShowDomainWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnNavigatedTo_DomainNoDC_ShowsNA()
    {
        var domainService = Substitute.For<IDomainService>();
        domainService.GetDomainInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new DomainInfo
            {
                JoinType = DomainJoinType.Workgroup,
                DomainOrWorkgroup = "WORKGROUP",
                DomainController = null
            });

        var vm = new CaptureConfigViewModel(_session, _navService, _dialogService, domainService: domainService);
        _session.SelectedItems = [];

        await vm.OnNavigatedTo();

        vm.DomainControllerDisplay.Should().Be("N/A");
    }

    #endregion
}
