using ZeroInstall.Agent.Models;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Agent.Tests.Models;

public class AgentOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new AgentOptions();

        options.Port.Should().Be(19850);
        options.Mode.Should().Be(AgentMode.Portable);
        options.SharedKey.Should().BeEmpty();
        options.DirectoryPath.Should().BeEmpty();
        options.PeerAddress.Should().BeNull();
    }

    [Fact]
    public void PropertyAssignment_Works()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = 12345,
            SharedKey = "mykey",
            Mode = AgentMode.Service,
            DirectoryPath = @"C:\capture",
            PeerAddress = "192.168.1.100"
        };

        options.Role.Should().Be(AgentRole.Source);
        options.Port.Should().Be(12345);
        options.SharedKey.Should().Be("mykey");
        options.Mode.Should().Be(AgentMode.Service);
        options.DirectoryPath.Should().Be(@"C:\capture");
        options.PeerAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public void AgentRole_HasExpectedValues()
    {
        Enum.GetValues<AgentRole>().Should().HaveCount(2);
        Enum.GetValues<AgentRole>().Should().Contain(AgentRole.Source);
        Enum.GetValues<AgentRole>().Should().Contain(AgentRole.Destination);
    }

    [Fact]
    public void DefaultRole_IsSource()
    {
        var options = new AgentOptions();
        options.Role.Should().Be(AgentRole.Source); // default enum value
    }
}
