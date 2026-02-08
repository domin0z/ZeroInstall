namespace ZeroInstall.Core.Enums;

/// <summary>
/// How the transfer agent runs on a machine.
/// </summary>
public enum AgentMode
{
    /// <summary>
    /// Standalone exe — runs only while transfer is active, no install needed.
    /// </summary>
    Portable,

    /// <summary>
    /// Installed as a Windows Service — survives reboots, runs unattended.
    /// </summary>
    Service
}
