namespace ZeroInstall.Agent.Models;

/// <summary>
/// Handshake message sent at the start of a transfer to authenticate and identify the peer.
/// </summary>
public class AgentHandshake
{
    /// <summary>
    /// Protocol version for forward compatibility.
    /// </summary>
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>
    /// Role of the agent sending the handshake.
    /// </summary>
    public AgentRole Role { get; set; }

    /// <summary>
    /// Shared key for authentication.
    /// </summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Hostname of the sending machine.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;
}
