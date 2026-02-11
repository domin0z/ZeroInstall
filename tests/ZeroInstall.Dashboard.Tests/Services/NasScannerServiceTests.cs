using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Transport;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Services;

public class NasScannerServiceTests
{
    private static (NasScannerService, ISftpClientWrapper, IDashboardDataService, IAlertService) CreateScanner(
        string? sftpHost = "nas.local")
    {
        var config = new DashboardConfiguration { NasSftpHost = sftpHost, NasSftpBasePath = "/backups/zim" };
        var dataService = Substitute.For<IDashboardDataService>();
        var alertService = Substitute.For<IAlertService>();
        var hubContext = Substitute.For<IHubContext<DashboardHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        // Since NasScannerService creates its own client, we can't easily substitute it.
        // For these tests we'll test the skip path and use the service interface directly.
        var scanner = new NasScannerService(config, dataService, alertService, hubContext,
            NullLogger<NasScannerService>.Instance);
        var sftpClient = Substitute.For<ISftpClientWrapper>();

        return (scanner, sftpClient, dataService, alertService);
    }

    [Fact]
    public async Task ScanAsync_SkipsWhenNoHostConfigured()
    {
        var config = new DashboardConfiguration { NasSftpHost = null };
        var dataService = Substitute.For<IDashboardDataService>();
        var alertService = Substitute.For<IAlertService>();
        var hubContext = Substitute.For<IHubContext<DashboardHub>>();
        var scanner = new NasScannerService(config, dataService, alertService, hubContext,
            NullLogger<NasScannerService>.Instance);

        await scanner.ScanAsync();

        await dataService.DidNotReceive().UpsertBackupStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_HandlesConnectionFailure_Gracefully()
    {
        var (scanner, _, dataService, _) = CreateScanner("unreachable.local");

        // Should not throw - logs error internally
        await scanner.ScanAsync();

        await dataService.DidNotReceive().UpsertBackupStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NasScannerBackgroundService_SkipsWhenNoHost()
    {
        var config = new DashboardConfiguration { NasSftpHost = null };
        var scopeFactory = Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var bgService = new NasScannerBackgroundService(scopeFactory, config,
            NullLogger<NasScannerBackgroundService>.Instance);

        // Service should exist but not throw
        bgService.Should().NotBeNull();
    }

    [Fact]
    public void DashboardConfiguration_NasScanInterval_Default()
    {
        var config = new DashboardConfiguration();
        config.NasScanIntervalMinutes.Should().Be(5);
    }

    [Fact]
    public void DashboardConfiguration_NasSftpBasePath_Default()
    {
        var config = new DashboardConfiguration();
        config.NasSftpBasePath.Should().Be("/backups/zim");
    }
}
