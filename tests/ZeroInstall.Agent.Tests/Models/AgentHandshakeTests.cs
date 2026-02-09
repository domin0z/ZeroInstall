using System.Text.Json;
using ZeroInstall.Agent.Models;

namespace ZeroInstall.Agent.Tests.Models;

public class AgentHandshakeTests
{
    [Fact]
    public void Handshake_DefaultProtocolVersion_Is1()
    {
        var handshake = new AgentHandshake();
        handshake.ProtocolVersion.Should().Be(1);
    }

    [Fact]
    public void Handshake_JsonRoundtrip()
    {
        var handshake = new AgentHandshake
        {
            ProtocolVersion = 1,
            Role = AgentRole.Destination,
            SharedKey = "secret123",
            Hostname = "WORKSTATION-01"
        };

        var json = JsonSerializer.Serialize(handshake);
        var deserialized = JsonSerializer.Deserialize<AgentHandshake>(json);

        deserialized.Should().NotBeNull();
        deserialized!.ProtocolVersion.Should().Be(1);
        deserialized.Role.Should().Be(AgentRole.Destination);
        deserialized.SharedKey.Should().Be("secret123");
        deserialized.Hostname.Should().Be("WORKSTATION-01");
    }

    [Fact]
    public void HandshakeResponse_Accepted_JsonRoundtrip()
    {
        var response = new AgentHandshakeResponse
        {
            Accepted = true,
            Hostname = "SERVER-01"
        };

        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<AgentHandshakeResponse>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Accepted.Should().BeTrue();
        deserialized.Reason.Should().BeNull();
        deserialized.Hostname.Should().Be("SERVER-01");
    }

    [Fact]
    public void HandshakeResponse_Rejected_JsonRoundtrip()
    {
        var response = new AgentHandshakeResponse
        {
            Accepted = false,
            Reason = "Invalid key",
            Hostname = "SERVER-01"
        };

        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<AgentHandshakeResponse>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Accepted.Should().BeFalse();
        deserialized.Reason.Should().Be("Invalid key");
    }
}
