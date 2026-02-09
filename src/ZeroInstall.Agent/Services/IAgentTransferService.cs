using ZeroInstall.Core.Models;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Orchestrates file transfer between source and destination agents.
/// </summary>
public interface IAgentTransferService
{
    /// <summary>
    /// Runs as the source: listens for connection, authenticates, then sends all files.
    /// </summary>
    Task RunAsSourceAsync(CancellationToken ct);

    /// <summary>
    /// Runs as the destination: connects to source, authenticates, then receives all files.
    /// </summary>
    Task RunAsDestinationAsync(CancellationToken ct);

    /// <summary>
    /// Raised when transfer progress changes.
    /// </summary>
    event Action<TransferProgress>? ProgressChanged;

    /// <summary>
    /// Raised when the agent status changes (e.g., "Waiting for connection...", "Authenticating...").
    /// </summary>
    event Action<string>? StatusChanged;
}
