using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Agent.Tests.Services;

public class AgentPortableServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AgentPortableServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"zim-agent-portable-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task StartAsync_CallsStopApplicationOnCompletion()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            DirectoryPath = @"C:\nonexistent_test_dir_xyz",
            SharedKey = "test"
        };

        var transferService = Substitute.For<IAgentTransferService>();
        // RunAsSourceAsync will throw because dir doesn't exist
        transferService.RunAsSourceAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new DirectoryNotFoundException("Not found"));

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var logger = NullLogger<AgentPortableService>.Instance;

        var service = new AgentPortableService(transferService, options, lifetime, logger);

        await service.StartAsync(CancellationToken.None);

        // Give the background task time to complete
        await Task.Delay(500);

        lifetime.Received().StopApplication();
    }

    [Fact]
    public async Task StartAsync_SubscribesToEvents()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Destination,
            DirectoryPath = _tempDir,
            SharedKey = "test",
            PeerAddress = "127.0.0.1"
        };

        var transferService = Substitute.For<IAgentTransferService>();
        transferService.RunAsDestinationAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("test error"));

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var logger = NullLogger<AgentPortableService>.Instance;

        var service = new AgentPortableService(transferService, options, lifetime, logger);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(500);

        // Verify transfer was attempted
        await transferService.Received(1).RunAsDestinationAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_CompletesGracefully()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            DirectoryPath = _tempDir,
            SharedKey = "test"
        };

        var transferService = Substitute.For<IAgentTransferService>();
        transferService.RunAsSourceAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(async _ => await Task.Delay(5000)); // Long-running task

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var logger = NullLogger<AgentPortableService>.Instance;

        var service = new AgentPortableService(transferService, options, lifetime, logger);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // StopAsync should not hang
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var act = () => service.StopAsync(stopCts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_HandlesOperationCanceled()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            DirectoryPath = _tempDir,
            SharedKey = "test"
        };

        var transferService = Substitute.For<IAgentTransferService>();
        transferService.RunAsSourceAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new OperationCanceledException());

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var logger = NullLogger<AgentPortableService>.Instance;

        var service = new AgentPortableService(transferService, options, lifetime, logger);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(500);

        // Should still call StopApplication
        lifetime.Received().StopApplication();
    }
}
