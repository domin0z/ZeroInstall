using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Services;

/// <summary>
/// Simple singleton implementation of <see cref="ISessionState"/>.
/// </summary>
internal sealed class SessionState : ISessionState
{
    public MachineRole Role { get; set; }
    public List<MigrationItem> SelectedItems { get; set; } = [];
    public List<UserMapping> UserMappings { get; set; } = [];
    public TransportMethod TransportMethod { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string InputPath { get; set; } = string.Empty;
    public MigrationJob? CurrentJob { get; set; }

    // Transport configuration
    public string NetworkSharePath { get; set; } = string.Empty;
    public string NetworkShareUsername { get; set; } = string.Empty;
    public string NetworkSharePassword { get; set; } = string.Empty;
    public int DirectWiFiPort { get; set; } = 19850;
    public string DirectWiFiSharedKey { get; set; } = string.Empty;

    public void Reset()
    {
        Role = default;
        SelectedItems = [];
        UserMappings = [];
        TransportMethod = default;
        OutputPath = string.Empty;
        InputPath = string.Empty;
        CurrentJob = null;
        NetworkSharePath = string.Empty;
        NetworkShareUsername = string.Empty;
        NetworkSharePassword = string.Empty;
        DirectWiFiPort = 19850;
        DirectWiFiSharedKey = string.Empty;
    }
}
