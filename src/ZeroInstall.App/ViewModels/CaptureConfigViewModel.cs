using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ZeroInstall.App.Services;
using ZeroInstall.Core.Enums;
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

    // NAS Browser state
    [ObservableProperty]
    private string _sftpCurrentBrowsePath = "/";

    [ObservableProperty]
    private bool _sftpIsConnected;

    [ObservableProperty]
    private string _sftpConnectionStatus = string.Empty;

    public ObservableCollection<SftpFileInfo> SftpBrowseItems { get; } = [];

    public ObservableCollection<TransportMethod> TransportMethods { get; } =
        [TransportMethod.ExternalStorage, TransportMethod.NetworkShare, TransportMethod.DirectWiFi, TransportMethod.Sftp];

    public CaptureConfigViewModel(ISessionState session, INavigationService navigationService, IDialogService dialogService)
    {
        _session = session;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    public override Task OnNavigatedTo()
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

        return Task.CompletedTask;
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
        _navigationService.NavigateTo<MigrationProgressViewModel>();
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
