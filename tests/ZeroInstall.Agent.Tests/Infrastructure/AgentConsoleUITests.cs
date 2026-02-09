using ZeroInstall.Agent.Infrastructure;
using ZeroInstall.Agent.Models;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Agent.Tests.Infrastructure;

public class AgentConsoleUITests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(1048576, "1.00 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void FormatBytes_FormatsCorrectly(long bytes, string expected)
    {
        AgentConsoleUI.FormatBytes(bytes).Should().Be(expected);
    }

    [Fact]
    public void FormatTimeSpan_Seconds()
    {
        AgentConsoleUI.FormatTimeSpan(TimeSpan.FromSeconds(45)).Should().Be("45s");
    }

    [Fact]
    public void FormatTimeSpan_Minutes()
    {
        AgentConsoleUI.FormatTimeSpan(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(15)))
            .Should().Be("3m 15s");
    }

    [Fact]
    public void FormatTimeSpan_Hours()
    {
        AgentConsoleUI.FormatTimeSpan(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)))
            .Should().Be("2h 30m");
    }

    [Fact]
    public void WriteHeader_DoesNotThrow()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = 19850,
            DirectoryPath = @"C:\test",
            PeerAddress = "192.168.1.100"
        };

        var act = () =>
        {
            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                AgentConsoleUI.WriteHeader(options);
            }
            finally
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
        };

        act.Should().NotThrow();
    }
}
