namespace ZeroInstall.Agent.Models;

/// <summary>
/// Response to a handshake indicating whether the connection was accepted.
/// </summary>
public class AgentHandshakeResponse
{
    /// <summary>
    /// Whether the handshake was accepted.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Reason for rejection, if not accepted.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Hostname of the responding machine.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;
}
