using System.IO;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Concrete implementation of <see cref="IBluetoothAdapter"/> using InTheHand.Net.Bluetooth (32feet.NET).
/// Wraps BluetoothClient, BluetoothListener, and BluetoothSecurity for RFCOMM communication.
/// </summary>
public sealed class BluetoothAdapter : IBluetoothAdapter
{
    private BluetoothListener? _listener;
    private BluetoothClient? _activeClient;

    public bool IsBluetoothAvailable
    {
        get
        {
            try
            {
                using var client = new BluetoothClient();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public string LocalDeviceName
    {
        get
        {
            try
            {
                var radio = BluetoothRadio.Default;
                return radio?.Name ?? Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }
    }

    public async Task<List<DiscoveredBluetoothDevice>> DiscoverDevicesAsync(
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var client = new BluetoothClient();
            var devices = client.DiscoverDevices(20);

            var result = new List<DiscoveredBluetoothDevice>();
            foreach (var device in devices)
            {
                ct.ThrowIfCancellationRequested();
                result.Add(new DiscoveredBluetoothDevice
                {
                    DeviceName = device.DeviceName ?? "Unknown",
                    Address = device.DeviceAddress.ToUInt64(),
                    AddressString = device.DeviceAddress.ToString(),
                    IsPaired = device.Authenticated,
                    IsZimService = false
                });
            }

            return result;
        }, ct);
    }

    public Task<bool> PairAsync(ulong deviceAddress, string? pin = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var address = new BluetoothAddress(deviceAddress);
            return BluetoothSecurity.PairRequest(address, pin);
        }, ct);
    }

    public async Task<Stream> ConnectAsync(ulong deviceAddress, Guid serviceGuid, CancellationToken ct = default)
    {
        var client = new BluetoothClient();
        _activeClient = client;

        var address = new BluetoothAddress(deviceAddress);
        var endpoint = new BluetoothEndPoint(address, serviceGuid);

        await Task.Run(() => client.Connect(endpoint), ct);
        return client.GetStream();
    }

    public async Task<Stream> AcceptConnectionAsync(Guid serviceGuid, CancellationToken ct = default)
    {
        _listener = new BluetoothListener(serviceGuid);
        _listener.Start();

        var client = await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                if (_listener.Pending())
                    return _listener.AcceptBluetoothClient();
                Thread.Sleep(100);
            }

            ct.ThrowIfCancellationRequested();
            return null!;
        }, ct);

        _activeClient = client;
        return client.GetStream();
    }

    public void Dispose()
    {
        _activeClient?.Dispose();
        _listener?.Stop();
    }
}
