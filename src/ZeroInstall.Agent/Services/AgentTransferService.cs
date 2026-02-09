using System.Net;
using Microsoft.Extensions.Logging;
using ZeroInstall.Agent.Models;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Implements file transfer between source and destination agents over DirectWiFiTransport.
/// </summary>
internal class AgentTransferService : IAgentTransferService
{
    private readonly AgentOptions _options;
    private readonly ILogger<AgentTransferService> _logger;

    public AgentTransferService(AgentOptions options, ILogger<AgentTransferService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event Action<TransferProgress>? ProgressChanged;
    public event Action<string>? StatusChanged;

    public async Task RunAsSourceAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_options.DirectoryPath))
            throw new DirectoryNotFoundException($"Source directory not found: {_options.DirectoryPath}");

        var endpoint = new IPEndPoint(IPAddress.Any, _options.Port);

        // Start UDP discovery responder in background (best-effort — may fail if port is in use)
        using var discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var discoveryTask = Task.Run(async () =>
        {
            try
            {
                await DirectWiFiTransport.RespondToDiscoveryAsync(discoveryCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UDP discovery responder failed (non-critical)");
            }
        }, ct);

        StatusChanged?.Invoke("Waiting for connection...");
        _logger.LogInformation("Source agent listening on port {Port}", _options.Port);

        await using var transport = new DirectWiFiTransport(endpoint, isServer: true,
            new Logger<DirectWiFiTransport>(_logger));

        var connected = await transport.TestConnectionAsync(ct);
        if (!connected)
            throw new InvalidOperationException("Failed to accept incoming connection");

        StatusChanged?.Invoke("Authenticating...");

        // Receive and validate handshake
        var handshake = await AgentProtocol.ReceiveHandshakeAsync(transport, ct);
        _logger.LogInformation("Handshake received from {Hostname}", handshake.Hostname);

        if (handshake.SharedKey != _options.SharedKey)
        {
            await AgentProtocol.SendHandshakeResponseAsync(transport, new AgentHandshakeResponse
            {
                Accepted = false,
                Reason = "Authentication failed: invalid key",
                Hostname = Environment.MachineName
            }, ct);
            throw new UnauthorizedAccessException("Peer provided an invalid key");
        }

        await AgentProtocol.SendHandshakeResponseAsync(transport, new AgentHandshakeResponse
        {
            Accepted = true,
            Hostname = Environment.MachineName
        }, ct);

        StatusChanged?.Invoke($"Connected to {handshake.Hostname}");

        // Enumerate files and build file list with relative paths
        var files = EnumerateFiles(_options.DirectoryPath);
        var fileEntries = BuildFileEntries(files, _options.DirectoryPath);
        var manifest = BuildManifest(fileEntries);

        await transport.SendManifestAsync(manifest, ct);
        _logger.LogInformation("Manifest sent: {Count} files, {Size} bytes", fileEntries.Count, manifest.TotalEstimatedSizeBytes);

        // Send all files in order
        long overallTransferred = 0;
        long overallTotal = fileEntries.Sum(f => f.SizeBytes);

        for (int i = 0; i < fileEntries.Count; i++)
        {
            var entry = fileEntries[i];
            var checksum = await ChecksumHelper.ComputeFileAsync(entry.FullPath, ct);

            var metadata = new TransferMetadata
            {
                RelativePath = entry.RelativePath,
                SizeBytes = entry.SizeBytes,
                Checksum = checksum,
                ChunkIndex = i,
                TotalChunks = fileEntries.Count
            };

            var progress = new Progress<TransferProgress>(p =>
            {
                p.OverallBytesTransferred = overallTransferred + p.CurrentItemBytesTransferred;
                p.OverallTotalBytes = overallTotal;
                ProgressChanged?.Invoke(p);
            });

            await using var fileStream = File.OpenRead(entry.FullPath);
            await transport.SendAsync(fileStream, metadata, progress, ct);

            overallTransferred += entry.SizeBytes;
            _logger.LogDebug("Sent {Path} ({Size} bytes)", entry.RelativePath, entry.SizeBytes);
        }

        StatusChanged?.Invoke("Transfer complete");

        // Stop discovery responder
        await discoveryCts.CancelAsync();
        await discoveryTask;
    }

    public async Task RunAsDestinationAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_options.DirectoryPath);

        IPEndPoint endpoint;
        if (!string.IsNullOrEmpty(_options.PeerAddress))
        {
            endpoint = new IPEndPoint(IPAddress.Parse(_options.PeerAddress), _options.Port);
            StatusChanged?.Invoke($"Connecting to {_options.PeerAddress}:{_options.Port}...");
        }
        else
        {
            StatusChanged?.Invoke("Discovering peers...");
            var peers = await DiscoverWithRetryAsync(ct);
            if (peers.Count == 0)
                throw new InvalidOperationException("No peers found on the local network");

            endpoint = peers[0].ToEndPoint();
            StatusChanged?.Invoke($"Found peer: {peers[0].Hostname} ({peers[0].Address})");
        }

        await using var transport = new DirectWiFiTransport(endpoint, isServer: false,
            new Logger<DirectWiFiTransport>(_logger));

        var connected = await transport.TestConnectionAsync(ct);
        if (!connected)
            throw new InvalidOperationException($"Failed to connect to {endpoint}");

        StatusChanged?.Invoke("Authenticating...");

        // Send handshake
        await AgentProtocol.SendHandshakeAsync(transport, new AgentHandshake
        {
            Role = AgentRole.Destination,
            SharedKey = _options.SharedKey,
            Hostname = Environment.MachineName
        }, ct);

        var response = await AgentProtocol.ReceiveHandshakeResponseAsync(transport, ct);
        if (!response.Accepted)
            throw new UnauthorizedAccessException($"Connection rejected: {response.Reason}");

        StatusChanged?.Invoke($"Connected to {response.Hostname}");

        // Receive manifest to know how many files to expect
        var manifest = await transport.ReceiveManifestAsync(ct);
        var expectedFileCount = manifest.Items.Count;
        _logger.LogInformation("Manifest received: {Count} files expected", expectedFileCount);

        long overallTotal = manifest.TotalEstimatedSizeBytes;
        long overallTransferred = 0;

        // Receive exactly the expected number of files
        for (int i = 0; i < expectedFileCount; i++)
        {
            // ReceiveAsync reads metadata header + data frame
            // The metadata.RelativePath we pass is a hint; the real metadata comes from the frame
            var placeholderMetadata = new TransferMetadata { RelativePath = $"file_{i}" };
            using var stream = await transport.ReceiveAsync(placeholderMetadata, ct);

            // Use the manifest item for the relative path (files sent in order)
            var relativePath = manifest.Items[i].DisplayName;
            var destPath = Path.Combine(_options.DirectoryPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);

            await using (var fileStream = File.Create(destPath))
            {
                ms.Position = 0;
                await ms.CopyToAsync(fileStream, ct);
            }

            overallTransferred += ms.Length;

            ProgressChanged?.Invoke(new TransferProgress
            {
                CurrentItemName = relativePath,
                CurrentItemIndex = i + 1,
                TotalItems = expectedFileCount,
                CurrentItemBytesTransferred = ms.Length,
                CurrentItemTotalBytes = ms.Length,
                OverallBytesTransferred = overallTransferred,
                OverallTotalBytes = overallTotal
            });

            _logger.LogDebug("Received {Path} ({Size} bytes)", relativePath, ms.Length);
        }

        StatusChanged?.Invoke("Transfer complete");
    }

    internal static List<string> EnumerateFiles(string directoryPath)
    {
        return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<FileEntry> BuildFileEntries(List<string> files, string baseDir)
    {
        return files.Select(f =>
        {
            var info = new FileInfo(f);
            return new FileEntry
            {
                FullPath = f,
                RelativePath = Path.GetRelativePath(baseDir, f),
                SizeBytes = info.Length
            };
        }).ToList();
    }

    internal static TransferManifest BuildManifest(List<FileEntry> entries)
    {
        var items = entries.Select(e => new MigrationItem
        {
            DisplayName = e.RelativePath,
            ItemType = MigrationItemType.FileGroup,
            IsSelected = true,
            EstimatedSizeBytes = e.SizeBytes
        }).ToList();

        return new TransferManifest
        {
            SourceHostname = Environment.MachineName,
            TransportMethod = TransportMethod.DirectWiFi,
            Items = items
        };
    }

    private static async Task<List<DiscoveredPeer>> DiscoverWithRetryAsync(CancellationToken ct, int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var peers = await DirectWiFiTransport.DiscoverPeersAsync(3000, ct);
            if (peers.Count > 0) return peers;
            if (attempt < maxAttempts)
                await Task.Delay(1000, ct);
        }
        return [];
    }
}

internal class FileEntry
{
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

/// <summary>
/// Adapter to create ILogger&lt;T&gt; from a non-generic ILogger.
/// </summary>
internal class Logger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    public Logger(ILogger inner) => _inner = inner;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
