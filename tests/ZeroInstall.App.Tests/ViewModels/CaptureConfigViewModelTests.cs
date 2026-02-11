using NSubstitute;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

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
    public void TransportMethods_HasFourEntries()
    {
        _sut.TransportMethods.Should().HaveCount(4);
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
}
