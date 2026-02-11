using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Configure capture — set output path, transport method, and review summary before starting.
/// </summary>
public partial class CaptureConfigViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IBitLockerService? _bitLockerService;
    private readonly IFirmwareService? _firmwareService;
    private readonly IDomainService? _domainService;
    private readonly ICrossPlatformDiscoveryService? _crossPlatformDiscovery;
    private ISftpClientWrapper? _sftpClient;

    public override string Title => "Configure";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private TransportMethod _selectedTransport;

    [ObservableProperty]
    private int _packageItemCount;

    [ObservableProperty]
    private int _regFileItemCount;

    [ObservableProperty]
    private int _profileItemCount;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    // Transport config — Network Share
    [ObservableProperty]
    private string _networkSharePath = string.Empty;

    [ObservableProperty]
    private string _networkShareUsername = string.Empty;

    [ObservableProperty]
    private string _networkSharePassword = string.Empty;

    // Transport config — Direct WiFi
    [ObservableProperty]
    private int _directWiFiPort = 19850;

    [ObservableProperty]
    private string _directWiFiSharedKey = string.Empty;

    // Transport config — SFTP
    [ObservableProperty]
    private string _sftpHost = string.Empty;

    [ObservableProperty]
    private int _sftpPort = 22;

    [ObservableProperty]
    private string _sftpUsername = string.Empty;

    [ObservableProperty]
    private string _sftpPassword = string.Empty;

    [ObservableProperty]
    private string _sftpPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sftpPrivateKeyPassphrase = string.Empty;

    [ObservableProperty]
    private string _sftpRemoteBasePath = "/backups/zim";

    [ObservableProperty]
    private string _sftpEncryptionPassphrase = string.Empty;

    [ObservableProperty]
    private bool _sftpCompressBeforeUpload = true;

    // BitLocker warning
    [ObservableProperty]
    private string _bitLockerWarning = string.Empty;

    [ObservableProperty]
    private bool _showBitLockerWarning;

    // Firmware info
    [ObservableProperty]
    private string _firmwareTypeDisplay = string.Empty;

    [ObservableProperty]
    private string _secureBootDisplay = string.Empty;

    [ObservableProperty]
    private string _tpmDisplay = string.Empty;

    [ObservableProperty]
    private string _biosInfoDisplay = string.Empty;

    [ObservableProperty]
    private string _systemInfoDisplay = string.Empty;

    [ObservableProperty]
    private bool _showFirmwareInfo;

    [ObservableProperty]
    private string _firmwareWarning = string.Empty;

    [ObservableProperty]
    private bool _showFirmwareWarning;

    [ObservableProperty]
    private bool _includeBcdBackup = true;

    // NAS Browser state
    [ObservableProperty]
    private string _sftpCurrentBrowsePath = "/";

    [ObservableProperty]
    private bool _sftpIsConnected;

    [ObservableProperty]
    private string _sftpConnectionStatus = string.Empty;

    // Transport config — Bluetooth
    [ObservableProperty]
    private string _bluetoothDeviceName = string.Empty;

    [ObservableProperty]
    private ulong _bluetoothDeviceAddress;

    [ObservableProperty]
    private bool _bluetoothIsServer;

    [ObservableProperty]
    private bool _bluetoothIsScanning;

    [ObservableProperty]
    private bool _bluetoothIsConnected;

    [ObservableProperty]
    private string _bluetoothConnectionStatus = string.Empty;

    [ObservableProperty]
    private string _bluetoothSpeedWarning = string.Empty;

    // Domain info
    [ObservableProperty]
    private string _domainJoinTypeDisplay = string.Empty;

    [ObservableProperty]
    private string _domainInfoDisplay = string.Empty;

    [ObservableProperty]
    private string _domainControllerDisplay = string.Empty;

    [ObservableProperty]
    private bool _showDomainInfo;

    [ObservableProperty]
    private string _domainWarning = string.Empty;

    [ObservableProperty]
    private bool _showDomainWarning;

    // Cross-platform source
    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private SourcePlatform _detectedPlatform;

    [ObservableProperty]
    private string _detectedPlatformDisplay = string.Empty;

    [ObservableProperty]
    private string? _detectedOsVersion;

    [ObservableProperty]
    private bool _showPlatformBanner;

    [ObservableProperty]
    private string _crossPlatformWarning = string.Empty;

    [ObservableProperty]
    private bool _showCrossPlatformWarning;

    public bool IsExternalSource => !string.IsNullOrEmpty(SourcePath);

    public ObservableCollection<DiscoveredBluetoothDevice> BluetoothDiscoveredDevices { get; } = [];

    public ObservableCollection<SftpFileInfo> SftpBrowseItems { get; } = [];

    public ObservableCollection<TransportMethod> TransportMethods { get; } =
        [TransportMethod.ExternalStorage, TransportMethod.NetworkShare, TransportMethod.DirectWiFi, TransportMethod.Sftp, TransportMethod.Bluetooth];

    public CaptureConfigViewModel(
        ISessionState session,
        INavigationService navigationService,
        IDialogService dialogService,
        IBitLockerService? bitLockerService = null,
        IFirmwareService? firmwareService = null,
        IDomainService? domainService = null,
        ICrossPlatformDiscoveryService? crossPlatformDiscovery = null)
    {
        _session = session;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _bitLockerService = bitLockerService;
        _firmwareService = firmwareService;
        _domainService = domainService;
        _crossPlatformDiscovery = crossPlatformDiscovery;
    }

    public override async Task OnNavigatedTo()
    {
        var items = _session.SelectedItems.Where(i => i.IsSelected).ToList();

        PackageItemCount = items.Count(i => i.EffectiveTier == MigrationTier.Package);
        RegFileItemCount = items.Count(i => i.EffectiveTier == MigrationTier.RegistryFile);
        ProfileItemCount = items.Count(i =>
            i.ItemType == MigrationItemType.UserProfile
            || i.ItemType == MigrationItemType.SystemSetting
            || i.ItemType == MigrationItemType.BrowserData);

        var totalBytes = items.Sum(i => i.EstimatedSizeBytes);
        TotalSizeFormatted = FormatBytes(totalBytes);

        // Restore cross-platform source path from session
        SourcePath = _session.SourcePath;
        if (!string.IsNullOrEmpty(SourcePath))
        {
            await DetectPlatformAsync();
        }

        // Restore transport config from session
        NetworkSharePath = _session.NetworkSharePath;
        NetworkShareUsername = _session.NetworkShareUsername;
        NetworkSharePassword = _session.NetworkSharePassword;
        DirectWiFiPort = _session.DirectWiFiPort;
        DirectWiFiSharedKey = _session.DirectWiFiSharedKey;
        SftpHost = _session.SftpHost;
        SftpPort = _session.SftpPort;
        SftpUsername = _session.SftpUsername;
        SftpPassword = _session.SftpPassword;
        SftpPrivateKeyPath = _session.SftpPrivateKeyPath;
        SftpPrivateKeyPassphrase = _session.SftpPrivateKeyPassphrase;
        SftpRemoteBasePath = _session.SftpRemoteBasePath;
        SftpEncryptionPassphrase = _session.SftpEncryptionPassphrase;
        SftpCompressBeforeUpload = _session.SftpCompressBeforeUpload;
        BluetoothDeviceName = _session.BluetoothDeviceName;
        BluetoothDeviceAddress = _session.BluetoothDeviceAddress;
        BluetoothIsServer = _session.BluetoothIsServer;
        IncludeBcdBackup = _session.IncludeBcdBackup;

        // Check BitLocker status on system drive
        await CheckBitLockerStatusAsync();

        // Check firmware info
        await CheckFirmwareInfoAsync();

        // Check domain info
        await CheckDomainInfoAsync();
    }

    private async Task CheckBitLockerStatusAsync()
    {
        if (_bitLockerService is null)
        {
            ShowBitLockerWarning = false;
            return;
        }

        try
        {
            var systemDrive = System.IO.Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var driveLetter = systemDrive.TrimEnd('\\');

            var status = await _bitLockerService.GetStatusAsync(driveLetter);

            if (status.ProtectionStatus == BitLockerProtectionStatus.Locked)
            {
                BitLockerWarning = $"BitLocker: Volume {driveLetter} is LOCKED. " +
                                   "Cloning will produce encrypted data that cannot be restored. " +
                                   "Unlock this volume before proceeding.";
                ShowBitLockerWarning = true;
            }
            else if (status.ProtectionStatus == BitLockerProtectionStatus.Unlocked)
            {
                BitLockerWarning = $"BitLocker: Volume {driveLetter} is encrypted (unlocked). " +
                                   "Data can be read, but suspending BitLocker protection before " +
                                   "cloning is recommended for best results.";
                ShowBitLockerWarning = true;
            }
            else
            {
                ShowBitLockerWarning = false;
            }
        }
        catch
        {
            ShowBitLockerWarning = false;
        }
    }

    private async Task CheckFirmwareInfoAsync()
    {
        if (_firmwareService is null)
        {
            ShowFirmwareInfo = false;
            ShowFirmwareWarning = false;
            return;
        }

        try
        {
            var info = await _firmwareService.GetFirmwareInfoAsync();

            FirmwareTypeDisplay = info.FirmwareType.ToString();
            SecureBootDisplay = info.SecureBoot.ToString();
            TpmDisplay = info.TpmPresent ? $"Yes (v{info.TpmVersion})" : "No";
            BiosInfoDisplay = !string.IsNullOrEmpty(info.BiosVendor)
                ? $"{info.BiosVendor} {info.BiosVersion}"
                : "N/A";
            SystemInfoDisplay = !string.IsNullOrEmpty(info.SystemManufacturer)
                ? $"{info.SystemManufacturer} {info.SystemModel}"
                : "N/A";

            ShowFirmwareInfo = true;
            FirmwareWarning = "BIOS/UEFI settings (boot order, virtualization, power management) are " +
                              "hardware-specific and cannot be migrated. Configure these manually on the " +
                              "destination machine. Only the BCD boot store can be backed up.";
            ShowFirmwareWarning = true;
        }
        catch
        {
            ShowFirmwareInfo = false;
            ShowFirmwareWarning = false;
        }
    }

    private async Task CheckDomainInfoAsync()
    {
        if (_domainService is null)
        {
            ShowDomainInfo = false;
            ShowDomainWarning = false;
            return;
        }

        try
        {
            var info = await _domainService.GetDomainInfoAsync();

            DomainJoinTypeDisplay = info.JoinType.ToString();
            DomainInfoDisplay = info.DomainOrWorkgroup;
            DomainControllerDisplay = !string.IsNullOrEmpty(info.DomainController)
                ? info.DomainController
                : "N/A";

            ShowDomainInfo = true;

            if (info.IsDomainJoined)
            {
                DomainWarning = "Domain policies, GPOs, and trust relationships cannot be migrated " +
                                "automatically. The destination machine must be joined to the domain separately.";
                ShowDomainWarning = true;
            }
            else
            {
                ShowDomainWarning = false;
            }
        }
        catch
        {
            ShowDomainInfo = false;
            ShowDomainWarning = false;
        }
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var path = await _dialogService.BrowseFolderAsync("Select Source Drive", SourcePath);
        if (path is not null)
        {
            SourcePath = path;
            _session.SourcePath = path;
            OnPropertyChanged(nameof(IsExternalSource));
            await DetectPlatformAsync();
        }
    }

    [RelayCommand]
    private async Task DetectPlatformAsync()
    {
        if (_crossPlatformDiscovery is null || string.IsNullOrEmpty(SourcePath))
        {
            ShowPlatformBanner = false;
            ShowCrossPlatformWarning = false;
            return;
        }

        try
        {
            DetectedPlatform = await _crossPlatformDiscovery.DetectSourcePlatformAsync(SourcePath);

            if (DetectedPlatform is SourcePlatform.MacOs or SourcePlatform.Linux)
            {
                var platformName = DetectedPlatform == SourcePlatform.MacOs ? "macOS" : "Linux";
                var result = await _crossPlatformDiscovery.DiscoverAllAsync(SourcePath);
                DetectedOsVersion = result.OsVersion;

                DetectedPlatformDisplay = !string.IsNullOrEmpty(DetectedOsVersion)
                    ? $"{platformName} source detected ({DetectedOsVersion})"
                    : $"{platformName} source detected";

                ShowPlatformBanner = true;

                CrossPlatformWarning = "Registry capture and full disk clone are not available for " +
                                       "cross-platform sources. Only package-based migration and " +
                                       "profile/file transfer are supported.";
                ShowCrossPlatformWarning = true;
            }
            else
            {
                ShowPlatformBanner = false;
                ShowCrossPlatformWarning = false;
            }
        }
        catch
        {
            ShowPlatformBanner = false;
            ShowCrossPlatformWarning = false;
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var path = await _dialogService.BrowseFolderAsync("Select Output Folder", OutputPath);
        if (path is not null)
        {
            OutputPath = path;
        }
    }

    [RelayCommand]
    private async Task SftpConnectAsync()
    {
        try
        {
            SftpConnectionStatus = "Connecting...";
            _sftpClient?.Dispose();
            _sftpClient = new SftpClientWrapper(
                SftpHost, SftpPort, SftpUsername,
                string.IsNullOrEmpty(SftpPassword) ? null : SftpPassword,
                string.IsNullOrEmpty(SftpPrivateKeyPath) ? null : SftpPrivateKeyPath,
                string.IsNullOrEmpty(SftpPrivateKeyPassphrase) ? null : SftpPrivateKeyPassphrase);
            _sftpClient.Connect();

            SftpIsConnected = true;
            SftpConnectionStatus = "Connected";
            SftpCurrentBrowsePath = SftpRemoteBasePath;
            await SftpBrowseToAsync(SftpRemoteBasePath);
        }
        catch (Exception ex)
        {
            SftpIsConnected = false;
            SftpConnectionStatus = $"Failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task SftpBrowseToAsync(string path)
    {
        if (_sftpClient is null || !_sftpClient.IsConnected)
            return Task.CompletedTask;

        try
        {
            SftpBrowseItems.Clear();
            SftpCurrentBrowsePath = path;

            var items = _sftpClient.ListDirectory(path);
            foreach (var item in items)
                SftpBrowseItems.Add(item);
        }
        catch (Exception ex)
        {
            SftpConnectionStatus = $"Browse error: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SftpCreateFolderAsync(string folderName)
    {
        if (_sftpClient is null || !_sftpClient.IsConnected || string.IsNullOrWhiteSpace(folderName))
            return Task.CompletedTask;

        try
        {
            var newPath = SftpCurrentBrowsePath.TrimEnd('/') + "/" + folderName;
            _sftpClient.CreateDirectory(newPath);
            return SftpBrowseToAsync(SftpCurrentBrowsePath);
        }
        catch (Exception ex)
        {
            SftpConnectionStatus = $"Create folder error: {ex.Message}";
            return Task.CompletedTask;
        }
    }

    [RelayCommand]
    private async Task SftpBrowseKeyFileAsync()
    {
        var path = await _dialogService.BrowseFolderAsync("Select SSH Key File", SftpPrivateKeyPath);
        if (path is not null)
        {
            SftpPrivateKeyPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private void StartCapture()
    {
        _session.SourcePath = SourcePath;
        _session.OutputPath = OutputPath;
        _session.TransportMethod = SelectedTransport;
        _session.NetworkSharePath = NetworkSharePath;
        _session.NetworkShareUsername = NetworkShareUsername;
        _session.NetworkSharePassword = NetworkSharePassword;
        _session.DirectWiFiPort = DirectWiFiPort;
        _session.DirectWiFiSharedKey = DirectWiFiSharedKey;
        _session.SftpHost = SftpHost;
        _session.SftpPort = SftpPort;
        _session.SftpUsername = SftpUsername;
        _session.SftpPassword = SftpPassword;
        _session.SftpPrivateKeyPath = SftpPrivateKeyPath;
        _session.SftpPrivateKeyPassphrase = SftpPrivateKeyPassphrase;
        _session.SftpRemoteBasePath = SftpRemoteBasePath;
        _session.SftpEncryptionPassphrase = SftpEncryptionPassphrase;
        _session.SftpCompressBeforeUpload = SftpCompressBeforeUpload;
        _session.BluetoothDeviceName = BluetoothDeviceName;
        _session.BluetoothDeviceAddress = BluetoothDeviceAddress;
        _session.BluetoothIsServer = BluetoothIsServer;
        _session.IncludeBcdBackup = IncludeBcdBackup;
        _navigationService.NavigateTo<MigrationProgressViewModel>();
    }

    [RelayCommand]
    private async Task BluetoothScanAsync()
    {
        BluetoothIsScanning = true;
        BluetoothDiscoveredDevices.Clear();
        BluetoothConnectionStatus = "Scanning for nearby devices...";

        try
        {
            var adapter = new BluetoothAdapter();
            var devices = await adapter.DiscoverDevicesAsync(TimeSpan.FromSeconds(10));
            foreach (var device in devices)
                BluetoothDiscoveredDevices.Add(device);

            BluetoothConnectionStatus = $"Found {devices.Count} device(s)";
        }
        catch (Exception ex)
        {
            BluetoothConnectionStatus = $"Scan failed: {ex.Message}";
        }
        finally
        {
            BluetoothIsScanning = false;
        }
    }

    [RelayCommand]
    private async Task BluetoothPairAndConnectAsync(DiscoveredBluetoothDevice? device)
    {
        if (device is null) return;

        BluetoothConnectionStatus = $"Pairing with {device.DeviceName}...";

        try
        {
            var adapter = new BluetoothAdapter();
            if (!device.IsPaired)
            {
                var paired = await adapter.PairAsync(device.Address);
                if (!paired)
                {
                    BluetoothConnectionStatus = "Pairing failed";
                    return;
                }
            }

            BluetoothDeviceName = device.DeviceName;
            BluetoothDeviceAddress = device.Address;
            BluetoothIsConnected = true;
            BluetoothConnectionStatus = $"Paired with {device.DeviceName}";

            // Calculate speed warning based on total data size
            var totalBytes = _session.SelectedItems
                .Where(i => i.IsSelected)
                .Sum(i => i.EstimatedSizeBytes);
            var estimate = BluetoothTransport.EstimateTransferTime(totalBytes);
            if (estimate.TotalMinutes > 5)
            {
                BluetoothSpeedWarning = $"Estimated transfer time: {estimate.TotalMinutes:F0} minutes at Bluetooth speeds (~250 KB/s)";
            }
        }
        catch (Exception ex)
        {
            BluetoothConnectionStatus = $"Connection failed: {ex.Message}";
        }
    }

    internal bool CanStartCapture() => !string.IsNullOrWhiteSpace(OutputPath);

    internal static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0
            ? $"{size:F0} {units[order]}"
            : $"{size:F1} {units[order]}";
    }
}
