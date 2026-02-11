namespace ZeroInstall.Core.Enums;

/// <summary>
/// How data is moved between the source and destination machines.
/// </summary>
public enum TransportMethod
{
    /// <summary>
    /// USB drive or external hard disk.
    /// </summary>
    ExternalStorage,

    /// <summary>
    /// SMB/UNC network share (e.g., NAS path).
    /// </summary>
    NetworkShare,

    /// <summary>
    /// Direct TCP transfer over WiFi between two machines running the agent.
    /// </summary>
    DirectWiFi,

    /// <summary>
    /// SFTP transfer to/from a remote NAS or server.
    /// </summary>
    Sftp,

    /// <summary>
    /// Bluetooth RFCOMM transfer between nearby machines.
    /// Slow (~250 KB/s) but requires no network infrastructure.
    /// </summary>
    Bluetooth
}
