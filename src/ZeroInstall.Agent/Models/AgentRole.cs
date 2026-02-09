namespace ZeroInstall.Agent.Models;

/// <summary>
/// Which side of the transfer this agent instance represents.
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// This machine has the captured data to send.
    /// </summary>
    Source,

    /// <summary>
    /// This machine receives and writes the transferred data.
    /// </summary>
    Destination
}
