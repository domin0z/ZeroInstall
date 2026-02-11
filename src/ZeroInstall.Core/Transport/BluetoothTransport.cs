using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Transfers data between two machines over Bluetooth RFCOMM.
/// Uses the same 4-byte length-prefix frame protocol as <see cref="DirectWiFiTransport"/>.
/// Bluetooth Classic RFCOMM tops out at ~150-250 KB/s -- suitable for small transfers
/// (settings, profiles, manifests) but impractical for multi-GB data.
/// </summary>
public class BluetoothTransport : ITransport, IAsyncDisposable
{
    private const int HeaderSizeBytes = 4;

    /// <summary>
    /// The Bluetooth service GUID used to identify the ZeroInstall Migrator RFCOMM service.
    /// </summary>
    internal static readonly Guid ServiceGuid = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

    /// <summary>
    /// Estimated maximum throughput for Bluetooth Classic RFCOMM in bytes per second.
    /// </summary>
    internal const int EstimatedMaxBytesPerSecond = 250 * 1024;

    private readonly IBluetoothAdapter _adapter;
    private readonly ulong _remoteAddress;
    private readonly bool _isServer;
    private readonly ILogger<BluetoothTransport> _logger;

    private Stream? _stream;

    /// <summary>
    /// Creates a BluetoothTransport instance.
    /// </summary>
    /// <param name="adapter">The Bluetooth adapter abstraction.</param>
    /// <param name="remoteAddress">The Bluetooth address of the remote device (0 for server mode).</param>
    /// <param name="isServer">True if this instance should listen for incoming connections.</param>
    /// <param name="logger">Logger instance.</param>
    public BluetoothTransport(
        IBluetoothAdapter adapter,
        ulong remoteAddress,
        bool isServer,
        ILogger<BluetoothTransport> logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _remoteAddress = remoteAddress;
        _isServer = isServer;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            if (_isServer)
            {
                _logger.LogInformation("Waiting for Bluetooth connection...");
                _stream = await _adapter.AcceptConnectionAsync(ServiceGuid, ct);
                _logger.LogInformation("Bluetooth client connected");
            }
            else
            {
                _logger.LogInformation("Connecting to Bluetooth device {Address}...", _remoteAddress);
                _stream = await _adapter.ConnectAsync(_remoteAddress, ServiceGuid, ct);
                _logger.LogInformation("Connected to Bluetooth device");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to establish Bluetooth connection");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        Stream data,
        TransferMetadata metadata,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        EnsureConnected();

        var metadataJson = JsonSerializer.Serialize(metadata);
        await SendFrameAsync(Encoding.UTF8.GetBytes(metadataJson), ct);

        using var buffer = new MemoryStream();
        await data.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await SendFrameAsync(buffer.ToArray(), ct);

        _logger.LogDebug("Sent {Path} ({Size} bytes) via Bluetooth", metadata.RelativePath, metadata.SizeBytes);
    }

    /// <inheritdoc/>
    public async Task<Stream> ReceiveAsync(TransferMetadata metadata, CancellationToken ct = default)
    {
        EnsureConnected();

        var headerBytes = await ReceiveFrameAsync(ct);
        var receivedMetadata = JsonSerializer.Deserialize<TransferMetadata>(Encoding.UTF8.GetString(headerBytes))
            ?? throw new InvalidDataException("Failed to deserialize transfer metadata");

        var dataBytes = await ReceiveFrameAsync(ct);

        _logger.LogDebug("Received {Path} ({Size} bytes) via Bluetooth",
            receivedMetadata.RelativePath, dataBytes.Length);

        return new MemoryStream(dataBytes);
    }

    /// <inheritdoc/>
    public async Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default)
    {
        EnsureConnected();

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await SendFrameAsync(Encoding.UTF8.GetBytes(json), ct);

        _logger.LogInformation("Transfer manifest sent via Bluetooth");
    }

    /// <inheritdoc/>
    public async Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default)
    {
        EnsureConnected();

        var frameBytes = await ReceiveFrameAsync(ct);
        var json = Encoding.UTF8.GetString(frameBytes);
        var manifest = JsonSerializer.Deserialize<TransferManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize transfer manifest");

        _logger.LogInformation("Transfer manifest received via Bluetooth");
        return manifest;
    }

    /// <summary>
    /// Discovers nearby Bluetooth devices via the adapter.
    /// </summary>
    public static async Task<List<DiscoveredBluetoothDevice>> DiscoverDevicesAsync(
        IBluetoothAdapter adapter,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        return await adapter.DiscoverDevicesAsync(timeout, ct);
    }

    /// <summary>
    /// Estimates the transfer time for a given total byte count at Bluetooth Classic speeds.
    /// </summary>
    public static TimeSpan EstimateTransferTime(long totalBytes)
    {
        if (totalBytes <= 0)
            return TimeSpan.Zero;

        var seconds = (double)totalBytes / EstimatedMaxBytesPerSecond;
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task SendFrameAsync(byte[] data, CancellationToken ct)
    {
        var lengthPrefix = BitConverter.GetBytes(data.Length);
        await _stream!.WriteAsync(lengthPrefix, ct);
        await _stream.WriteAsync(data, ct);
        await _stream.FlushAsync(ct);
    }

    private async Task<byte[]> ReceiveFrameAsync(CancellationToken ct)
    {
        var lengthBuffer = new byte[HeaderSizeBytes];
        await ReadExactAsync(_stream!, lengthBuffer, ct);
        var length = BitConverter.ToInt32(lengthBuffer);

        if (length <= 0 || length > 500 * 1024 * 1024)
            throw new InvalidDataException($"Invalid frame size: {length}");

        var data = new byte[length];
        await ReadExactAsync(_stream!, data, ct);
        return data;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0)
                throw new EndOfStreamException("Connection closed unexpectedly");
            offset += read;
        }
    }

    private void EnsureConnected()
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected. Call TestConnectionAsync first.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
            await _stream.DisposeAsync();
        _adapter.Dispose();

        GC.SuppressFinalize(this);
    }
}
