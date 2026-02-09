using ZeroInstall.App.ViewModels;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.App.Services;

/// <summary>
/// Shared state passed between views during a single migration workflow.
/// </summary>
public interface ISessionState
{
    MachineRole Role { get; set; }
    List<MigrationItem> SelectedItems { get; set; }
    List<UserMapping> UserMappings { get; set; }
    TransportMethod TransportMethod { get; set; }
    string OutputPath { get; set; }
    string InputPath { get; set; }
    MigrationJob? CurrentJob { get; set; }

    /// <summary>
    /// Clears all state for a new migration.
    /// </summary>
    void Reset();
}
