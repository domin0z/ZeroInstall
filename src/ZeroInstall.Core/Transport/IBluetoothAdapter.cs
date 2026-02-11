using System.IO;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Testability abstraction over Bluetooth hardware operations.
/// Allows unit tests to mock Bluetooth interactions without a real adapter.
/// </summary>
public interface IBluetoothAdapter : IDisposable
{
    /// <summary>
    /// Whether a Bluetooth adapter is present and enabled on this machine.
    /// </summary>
    bool IsBluetoothAvailable { get; }

    /// <summary>
    /// The friendly name of the local Bluetooth adapter/device.
    /// </summary>
    string LocalDeviceName { get; }

    /// <summary>
    /// Discovers nearby Bluetooth devices.
    /// </summary>
    Task<List<DiscoveredBluetoothDevice>> DiscoverDevicesAsync(
        TimeSpan timeout,
        CancellationToken ct = default);

    /// <summary>
    /// Pairs with a remote Bluetooth device by address.
    /// </summary>
    Task<bool> PairAsync(ulong deviceAddress, string? pin = null, CancellationToken ct = default);

    /// <summary>
    /// Connects to a remote Bluetooth device running the ZIM service (client mode).
    /// Returns the connected stream for data transfer.
    /// </summary>
    Task<Stream> ConnectAsync(ulong deviceAddress, Guid serviceGuid, CancellationToken ct = default);

    /// <summary>
    /// Listens for and accepts an incoming Bluetooth connection (server mode).
    /// Returns the connected stream for data transfer.
    /// </summary>
    Task<Stream> AcceptConnectionAsync(Guid serviceGuid, CancellationToken ct = default);
}
