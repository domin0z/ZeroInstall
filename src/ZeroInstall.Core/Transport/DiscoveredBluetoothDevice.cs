namespace ZeroInstall.Core.Transport;

/// <summary>
/// A Bluetooth device discovered during scanning.
/// </summary>
public class DiscoveredBluetoothDevice
{
    /// <summary>
    /// The friendly name of the Bluetooth device.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// The Bluetooth hardware address as a 48-bit unsigned integer.
    /// </summary>
    public ulong Address { get; set; }

    /// <summary>
    /// The Bluetooth address formatted as a colon-separated hex string (e.g., "AA:BB:CC:DD:EE:FF").
    /// </summary>
    public string AddressString { get; set; } = string.Empty;

    /// <summary>
    /// Whether this device is already paired with the local adapter.
    /// </summary>
    public bool IsPaired { get; set; }

    /// <summary>
    /// Whether this device advertises the ZeroInstall Migrator Bluetooth service.
    /// </summary>
    public bool IsZimService { get; set; }
}
