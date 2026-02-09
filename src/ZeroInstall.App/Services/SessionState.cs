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

    public void Reset()
    {
        Role = default;
        SelectedItems = [];
        UserMappings = [];
        TransportMethod = default;
        OutputPath = string.Empty;
        InputPath = string.Empty;
        CurrentJob = null;
    }
}
