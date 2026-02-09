using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroInstall.Agent.Infrastructure;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Agent.Tests.Infrastructure;

public class AgentHostTests : IDisposable
{
    private readonly string _tempDir;

    public AgentHostTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-agent-host-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void BuildHost_PortableMode_CreatesValidHost()
    {
        var options = new AgentOptions
        {
            Mode = AgentMode.Portable,
            Role = AgentRole.Source,
            DirectoryPath = _tempDir,
            SharedKey = "test"
        };

        using var host = AgentHost.BuildHost(options);
        host.Should().NotBeNull();
    }

    [Fact]
    public void BuildHost_PortableMode_ResolvesServices()
    {
        var options = new AgentOptions
        {
            Mode = AgentMode.Portable,
            Role = AgentRole.Source,
            DirectoryPath = _tempDir,
            SharedKey = "test"
        };

        using var host = AgentHost.BuildHost(options);

        var transferService = host.Services.GetService<IAgentTransferService>();
        transferService.Should().NotBeNull();

        var resolvedOptions = host.Services.GetService<AgentOptions>();
        resolvedOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void BuildHost_ServiceMode_CreatesValidHost()
    {
        var options = new AgentOptions
        {
            Mode = AgentMode.Service,
            Role = AgentRole.Source,
            DirectoryPath = _tempDir,
            SharedKey = "test"
        };

        using var host = AgentHost.BuildHost(options);
        host.Should().NotBeNull();

        var transferService = host.Services.GetService<IAgentTransferService>();
        transferService.Should().NotBeNull();
    }
}
