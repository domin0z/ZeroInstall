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

    // Transport configuration
    string NetworkSharePath { get; set; }
    string NetworkShareUsername { get; set; }
    string NetworkSharePassword { get; set; }
    int DirectWiFiPort { get; set; }
    string DirectWiFiSharedKey { get; set; }

    // SFTP configuration
    string SftpHost { get; set; }
    int SftpPort { get; set; }
    string SftpUsername { get; set; }
    string SftpPassword { get; set; }
    string SftpPrivateKeyPath { get; set; }
    string SftpPrivateKeyPassphrase { get; set; }
    string SftpRemoteBasePath { get; set; }
    string SftpEncryptionPassphrase { get; set; }
    bool SftpCompressBeforeUpload { get; set; }

    /// <summary>
    /// Clears all state for a new migration.
    /// </summary>
    void Reset();
}
