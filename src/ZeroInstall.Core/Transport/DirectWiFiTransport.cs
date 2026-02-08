using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// Transfers data directly between two machines over a TCP socket connection.
/// One machine acts as the server (listener), the other as the client.
/// Supports chunked transfer with progress reporting and bandwidth throttling.
/// </summary>
public class DirectWiFiTransport : ITransport, IAsyncDisposable
{
    private const int DefaultPort = 19850;
    private const int HeaderSizeBytes = 4; // 4-byte length prefix for framing

    private readonly IPEndPoint _remoteEndpoint;
    private readonly bool _isServer;
    private readonly int? _maxBytesPerSecond;
    private readonly ILogger<DirectWiFiTransport> _logger;

    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;

    /// <summary>
    /// Creates a DirectWiFiTransport instance.
    /// </summary>
    /// <param name="remoteEndpoint">The remote endpoint to connect to (client) or listen on (server).</param>
    /// <param name="isServer">True if this instance should listen for incoming connections.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxBytesPerSecond">Optional bandwidth throttle in bytes per second.</param>
    public DirectWiFiTransport(
        IPEndPoint remoteEndpoint,
        bool isServer,
        ILogger<DirectWiFiTransport> logger,
        int? maxBytesPerSecond = null)
    {
        _remoteEndpoint = remoteEndpoint ?? throw new ArgumentNullException(nameof(remoteEndpoint));
        _isServer = isServer;
        _logger = logger;
        _maxBytesPerSecond = maxBytesPerSecond;
    }

    /// <summary>
    /// Default port used for direct WiFi transfers.
    /// </summary>
    public static int Port => DefaultPort;

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            if (_isServer)
            {
                _listener = new TcpListener(_remoteEndpoint);
                _listener.Start();
                _logger.LogInformation("Listening for connection on {Endpoint}", _remoteEndpoint);

                _client = await _listener.AcceptTcpClientAsync(ct);
                _stream = _client.GetStream();

                _logger.LogInformation("Client connected from {Remote}", _client.Client.RemoteEndPoint);
            }
            else
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_remoteEndpoint, ct);
                _stream = _client.GetStream();

                _logger.LogInformation("Connected to server at {Endpoint}", _remoteEndpoint);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to establish direct WiFi connection");
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

        // Send metadata header
        var metadataJson = JsonSerializer.Serialize(metadata);
        await SendFrameAsync(Encoding.UTF8.GetBytes(metadataJson), ct);

        // Send data with progress
        using var buffer = new MemoryStream();
        await StreamCopyHelper.CopyWithProgressAsync(
            data, buffer,
            totalBytes: metadata.SizeBytes,
            itemName: metadata.RelativePath,
            itemIndex: metadata.ChunkIndex + 1,
            totalItems: metadata.TotalChunks,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: metadata.SizeBytes,
            progress,
            maxBytesPerSecond: _maxBytesPerSecond,
            ct: ct);

        buffer.Position = 0;
        await SendFrameAsync(buffer.ToArray(), ct);

        _logger.LogDebug("Sent {Path} ({Size} bytes) via direct WiFi", metadata.RelativePath, metadata.SizeBytes);
    }

    /// <inheritdoc/>
    public async Task<Stream> ReceiveAsync(TransferMetadata metadata, CancellationToken ct = default)
    {
        EnsureConnected();

        // Receive metadata header
        var headerBytes = await ReceiveFrameAsync(ct);
        var receivedMetadata = JsonSerializer.Deserialize<TransferMetadata>(Encoding.UTF8.GetString(headerBytes))
            ?? throw new InvalidDataException("Failed to deserialize transfer metadata");

        // Receive data
        var dataBytes = await ReceiveFrameAsync(ct);

        _logger.LogDebug("Received {Path} ({Size} bytes) via direct WiFi",
            receivedMetadata.RelativePath, dataBytes.Length);

        return new MemoryStream(dataBytes);
    }

    /// <inheritdoc/>
    public async Task SendManifestAsync(TransferManifest manifest, CancellationToken ct = default)
    {
        EnsureConnected();

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await SendFrameAsync(Encoding.UTF8.GetBytes(json), ct);

        _logger.LogInformation("Transfer manifest sent via direct WiFi");
    }

    /// <inheritdoc/>
    public async Task<TransferManifest> ReceiveManifestAsync(CancellationToken ct = default)
    {
        EnsureConnected();

        var frameBytes = await ReceiveFrameAsync(ct);
        var json = Encoding.UTF8.GetString(frameBytes);
        var manifest = JsonSerializer.Deserialize<TransferManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize transfer manifest");

        _logger.LogInformation("Transfer manifest received via direct WiFi");
        return manifest;
    }

    /// <summary>
    /// Discovers other ZeroInstall agents on the local network using a UDP broadcast.
    /// </summary>
    public static async Task<List<DiscoveredPeer>> DiscoverPeersAsync(
        int timeoutMs = 3000,
        CancellationToken ct = default)
    {
        var peers = new List<DiscoveredPeer>();
        var discoveryPort = DefaultPort + 1;

        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        udp.EnableBroadcast = true;

        // Send discovery broadcast
        var message = Encoding.UTF8.GetBytes($"ZIM-DISCOVER|{Environment.MachineName}");
        await udp.SendAsync(message, new IPEndPoint(IPAddress.Broadcast, discoveryPort), ct);

        // Listen for responses
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(cts.Token);
                var response = Encoding.UTF8.GetString(result.Buffer);

                if (response.StartsWith("ZIM-RESPONSE|"))
                {
                    var hostname = response["ZIM-RESPONSE|".Length..];
                    peers.Add(new DiscoveredPeer
                    {
                        Hostname = hostname,
                        Address = result.RemoteEndPoint.Address,
                        Port = DefaultPort
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout — expected
        }

        return peers;
    }

    /// <summary>
    /// Starts listening for discovery broadcasts and responds to them.
    /// </summary>
    public static async Task RespondToDiscoveryAsync(CancellationToken ct)
    {
        var discoveryPort = DefaultPort + 1;

        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, discoveryPort));
        udp.EnableBroadcast = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith("ZIM-DISCOVER|"))
                {
                    var response = Encoding.UTF8.GetBytes($"ZIM-RESPONSE|{Environment.MachineName}");
                    await udp.SendAsync(response, result.RemoteEndPoint, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
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

        if (length <= 0 || length > 500 * 1024 * 1024) // 500 MB max frame size
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
        _client?.Dispose();
        _listener?.Stop();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A peer discovered on the local network via UDP broadcast.
/// </summary>
public class DiscoveredPeer
{
    public string Hostname { get; set; } = string.Empty;
    public IPAddress Address { get; set; } = IPAddress.None;
    public int Port { get; set; }

    public IPEndPoint ToEndPoint() => new(Address, Port);
}
