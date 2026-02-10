using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class DirectWiFiTransportTests
{
    [Fact]
    public async Task SendAndReceiveManifest_OverLoopback()
    {
        var port = GetFreePort();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(serverEndpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(serverEndpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        var manifest = new TransferManifest
        {
            SourceHostname = "WIFI-SOURCE",
            TransportMethod = TransportMethod.DirectWiFi,
            Items =
            [
                new MigrationItem { DisplayName = "TestApp", ItemType = MigrationItemType.Application, IsSelected = true }
            ]
        };

        // Connect both sides concurrently
        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100); // Give server time to start listening
        var clientConnect = client.TestConnectionAsync();

        var results = await Task.WhenAll(serverConnect, clientConnect);
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        // Client sends manifest, server receives
        var sendTask = client.SendManifestAsync(manifest);
        var receiveTask = server.ReceiveManifestAsync();

        await Task.WhenAll(sendTask, receiveTask);
        var received = await receiveTask;

        received.SourceHostname.Should().Be("WIFI-SOURCE");
        received.TransportMethod.Should().Be(TransportMethod.DirectWiFi);
        received.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendAndReceiveData_OverLoopback()
    {
        var port = GetFreePort();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(serverEndpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(serverEndpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        // Connect
        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        var data = Encoding.UTF8.GetBytes("Hello from client via WiFi!");
        var metadata = new TransferMetadata
        {
            RelativePath = "wifi-test.txt",
            SizeBytes = data.Length,
            Checksum = ChecksumHelper.Compute(data)
        };

        // Client sends data, server receives
        using var sendStream = new MemoryStream(data);
        var sendTask = client.SendAsync(sendStream, metadata);
        var receiveTask = server.ReceiveAsync(metadata);

        await Task.WhenAll(sendTask, receiveTask);

        await using var receivedStream = await receiveTask;
        using var ms = new MemoryStream();
        await receivedStream.CopyToAsync(ms);

        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidEndpoint_ReturnsFalse()
    {
        // Try to connect to a port that nobody is listening on
        var port = GetFreePort();
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var client = new DirectWiFiTransport(endpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await client.TestConnectionAsync(cts.Token);

        result.Should().BeFalse();
    }

    [Fact]
    public void Port_ReturnsDefaultPort()
    {
        DirectWiFiTransport.Port.Should().Be(19850);
    }

    [Fact]
    public async Task SendAndReceiveMultipleFiles_OverLoopback()
    {
        var port = GetFreePort();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(serverEndpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(serverEndpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        // Connect
        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        // Send 3 files sequentially
        var files = new[]
        {
            ("file1.txt", "First file content"),
            ("file2.txt", "Second file content here"),
            ("file3.txt", "Third!")
        };

        foreach (var (name, content) in files)
        {
            var data = Encoding.UTF8.GetBytes(content);
            var metadata = new TransferMetadata
            {
                RelativePath = name,
                SizeBytes = data.Length,
                Checksum = ChecksumHelper.Compute(data)
            };

            using var sendStream = new MemoryStream(data);
            var sendTask = client.SendAsync(sendStream, metadata);
            var receiveTask = server.ReceiveAsync(metadata);

            await Task.WhenAll(sendTask, receiveTask);

            await using var receivedStream = await receiveTask;
            using var ms = new MemoryStream();
            await receivedStream.CopyToAsync(ms);

            Encoding.UTF8.GetString(ms.ToArray()).Should().Be(content);
        }
    }

    [Fact]
    public async Task LargePayload_TransfersCorrectly()
    {
        var port = GetFreePort();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(serverEndpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(serverEndpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        // Connect
        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        // 100KB payload
        var data = new byte[100 * 1024];
        Random.Shared.NextBytes(data);
        var checksum = ChecksumHelper.Compute(data);

        var metadata = new TransferMetadata
        {
            RelativePath = "large-payload.bin",
            SizeBytes = data.Length,
            Checksum = checksum
        };

        using var sendStream = new MemoryStream(data);
        var sendTask = client.SendAsync(sendStream, metadata);
        var receiveTask = server.ReceiveAsync(metadata);

        await Task.WhenAll(sendTask, receiveTask);

        await using var receivedStream = await receiveTask;
        using var ms = new MemoryStream();
        await receivedStream.CopyToAsync(ms);

        var receivedData = ms.ToArray();
        receivedData.Length.Should().Be(data.Length);
        ChecksumHelper.Compute(receivedData).Should().Be(checksum);
    }

    [Fact]
    public async Task TestConnectionAsync_ServerStartsListening()
    {
        var port = GetFreePort();
        var serverEndpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(serverEndpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(serverEndpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        // Both should connect successfully
        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();

        var results = await Task.WhenAll(serverConnect, clientConnect);
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
