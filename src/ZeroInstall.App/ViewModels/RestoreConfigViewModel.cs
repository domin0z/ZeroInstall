using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.App.ViewModels;

/// <summary>
/// Configure restore — select capture location, review source info, and map users.
/// </summary>
public partial class RestoreConfigViewModel : ViewModelBase
{
    private readonly ISessionState _session;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private ISftpClientWrapper? _sftpClient;

    public override string Title => "Restore";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCaptureCommand))]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private bool _hasValidCapture;

    [ObservableProperty]
    private string _captureInfo = string.Empty;

    // SFTP connection properties
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

    // NAS Browser state
    [ObservableProperty]
    private string _sftpCurrentBrowsePath = "/";

    [ObservableProperty]
    private bool _sftpIsConnected;

    [ObservableProperty]
    private string _sftpConnectionStatus = string.Empty;

    [ObservableProperty]
    private TransportMethod _selectedTransport;

    // Transport config — Bluetooth
    [ObservableProperty]
    private string _bluetoothDeviceName = string.Empty;

    [ObservableProperty]
    private ulong _bluetoothDeviceAddress;

    [ObservableProperty]
    private bool _bluetoothIsServer = true;

    [ObservableProperty]
    private bool _bluetoothIsScanning;

    [ObservableProperty]
    private bool _bluetoothIsConnected;

    [ObservableProperty]
    private string _bluetoothConnectionStatus = string.Empty;

    [ObservableProperty]
    private string _bluetoothSpeedWarning = string.Empty;

    public ObservableCollection<DiscoveredBluetoothDevice> BluetoothDiscoveredDevices { get; } = [];

    public ObservableCollection<SftpFileInfo> SftpBrowseItems { get; } = [];

    public ObservableCollection<UserMappingEntryViewModel> UserMappings { get; } = [];

    public RestoreConfigViewModel(ISessionState session, INavigationService navigationService, IDialogService dialogService)
    {
        _session = session;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    public override Task OnNavigatedTo()
    {
        if (!string.IsNullOrEmpty(_session.InputPath))
        {
            InputPath = _session.InputPath;
        }

        SelectedTransport = _session.TransportMethod;
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

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseInputAsync()
    {
        var path = await _dialogService.BrowseFolderAsync("Select Capture Folder", InputPath);
        if (path is not null)
        {
            InputPath = path;
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

    [RelayCommand(CanExecute = nameof(CanLoadCapture))]
    private async Task LoadCaptureAsync()
    {
        HasValidCapture = false;
        CaptureInfo = string.Empty;
        UserMappings.Clear();

        var profileSettingsDir = Path.Combine(InputPath, "profile-settings");
        var manifestPath = Path.Combine(profileSettingsDir, "profile-settings-manifest.json");

        if (!File.Exists(manifestPath))
        {
            CaptureInfo = "No valid capture found at this location.";
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ProfileSettingsManifest>(json);

            if (manifest is null)
            {
                CaptureInfo = "Could not read capture manifest.";
                return;
            }

            CaptureInfo = $"Captured on {manifest.CapturedUtc:yyyy-MM-dd HH:mm}";
            HasValidCapture = true;

            // Populate user mappings — look for profile subdirs
            var profilesDir = Path.Combine(profileSettingsDir, "profiles");
            if (Directory.Exists(profilesDir))
            {
                foreach (var userDir in Directory.GetDirectories(profilesDir))
                {
                    var username = Path.GetFileName(userDir);
                    var mapping = new UserMapping
                    {
                        SourceUser = new UserProfile
                        {
                            Username = username,
                            ProfilePath = $@"C:\Users\{username}"
                        },
                        DestinationUsername = username,
                        DestinationProfilePath = $@"C:\Users\{username}"
                    };
                    UserMappings.Add(new UserMappingEntryViewModel(mapping));
                }
            }

            StartRestoreCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            CaptureInfo = $"Error reading capture: {ex.Message}";
        }
    }

    private bool CanLoadCapture() => !string.IsNullOrWhiteSpace(InputPath);

    [RelayCommand(CanExecute = nameof(CanStartRestore))]
    private void StartRestore()
    {
        _session.InputPath = InputPath;
        _session.UserMappings = UserMappings.Select(vm => vm.Model).ToList();
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
        }
        catch (Exception ex)
        {
            BluetoothConnectionStatus = $"Connection failed: {ex.Message}";
        }
    }

    internal bool CanStartRestore() => HasValidCapture;
}
