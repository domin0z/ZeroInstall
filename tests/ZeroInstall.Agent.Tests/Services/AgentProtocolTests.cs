using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Agent.Tests.Services;

public class AgentProtocolTests
{
    [Fact]
    public async Task SendAndReceiveHandshake_OverLoopback()
    {
        var port = GetFreePort();
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(endpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(endpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        var handshake = new AgentHandshake
        {
            ProtocolVersion = 1,
            Role = AgentRole.Destination,
            SharedKey = "test-key-123",
            Hostname = "TEST-PC"
        };

        var sendTask = AgentProtocol.SendHandshakeAsync(client, handshake, CancellationToken.None);
        var receiveTask = AgentProtocol.ReceiveHandshakeAsync(server, CancellationToken.None);

        await Task.WhenAll(sendTask, receiveTask);
        var received = await receiveTask;

        received.ProtocolVersion.Should().Be(1);
        received.Role.Should().Be(AgentRole.Destination);
        received.SharedKey.Should().Be("test-key-123");
        received.Hostname.Should().Be("TEST-PC");
    }

    [Fact]
    public async Task SendAndReceiveHandshakeResponse_OverLoopback()
    {
        var port = GetFreePort();
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(endpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(endpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        var response = new AgentHandshakeResponse
        {
            Accepted = true,
            Hostname = "SERVER-PC"
        };

        var sendTask = AgentProtocol.SendHandshakeResponseAsync(server, response, CancellationToken.None);
        var receiveTask = AgentProtocol.ReceiveHandshakeResponseAsync(client, CancellationToken.None);

        await Task.WhenAll(sendTask, receiveTask);
        var received = await receiveTask;

        received.Accepted.Should().BeTrue();
        received.Hostname.Should().Be("SERVER-PC");
    }

    [Fact]
    public async Task SendAndReceiveRejectedResponse_OverLoopback()
    {
        var port = GetFreePort();
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);

        await using var server = new DirectWiFiTransport(endpoint, isServer: true,
            NullLogger<DirectWiFiTransport>.Instance);
        await using var client = new DirectWiFiTransport(endpoint, isServer: false,
            NullLogger<DirectWiFiTransport>.Instance);

        var serverConnect = server.TestConnectionAsync();
        await Task.Delay(100);
        var clientConnect = client.TestConnectionAsync();
        await Task.WhenAll(serverConnect, clientConnect);

        var response = new AgentHandshakeResponse
        {
            Accepted = false,
            Reason = "Bad key",
            Hostname = "SERVER-PC"
        };

        var sendTask = AgentProtocol.SendHandshakeResponseAsync(server, response, CancellationToken.None);
        var receiveTask = AgentProtocol.ReceiveHandshakeResponseAsync(client, CancellationToken.None);

        await Task.WhenAll(sendTask, receiveTask);
        var received = await receiveTask;

        received.Accepted.Should().BeFalse();
        received.Reason.Should().Be("Bad key");
    }

    [Fact]
    public void IsCompletionFrame_WithCompletionPath_ReturnsTrue()
    {
        var metadata = new TransferMetadata { RelativePath = AgentProtocol.TransferCompletePath };
        AgentProtocol.IsCompletionFrame(metadata).Should().BeTrue();
    }

    [Fact]
    public void IsCompletionFrame_WithRegularPath_ReturnsFalse()
    {
        var metadata = new TransferMetadata { RelativePath = "some/file.txt" };
        AgentProtocol.IsCompletionFrame(metadata).Should().BeFalse();
    }

    [Fact]
    public void SentinelPaths_AreDistinct()
    {
        var paths = new[] { AgentProtocol.HandshakePath, AgentProtocol.HandshakeResponsePath, AgentProtocol.TransferCompletePath };
        paths.Should().OnlyHaveUniqueItems();
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
