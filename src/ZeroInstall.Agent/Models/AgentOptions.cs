using ZeroInstall.Core.Enums;

namespace ZeroInstall.Agent.Models;

/// <summary>
/// Configuration options for the transfer agent.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Whether this agent is the source (sender) or destination (receiver).
    /// </summary>
    public AgentRole Role { get; set; }

    /// <summary>
    /// TCP port for the transfer connection.
    /// </summary>
    public int Port { get; set; } = 19850;

    /// <summary>
    /// Shared authentication key. Both sides must use the same key.
    /// </summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether to run as portable (console) or Windows Service.
    /// </summary>
    public AgentMode Mode { get; set; } = AgentMode.Portable;

    /// <summary>
    /// Directory containing captured data (source) or where to write received data (destination).
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional peer address to connect to directly (skips UDP discovery).
    /// </summary>
    public string? PeerAddress { get; set; }
}
