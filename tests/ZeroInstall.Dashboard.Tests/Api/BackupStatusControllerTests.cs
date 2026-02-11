using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using ZeroInstall.Dashboard.Api;
using ZeroInstall.Dashboard.Data.Entities;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Api;

public class BackupStatusControllerTests
{
    private static (BackupStatusController, IDashboardDataService, IAlertService) CreateController()
    {
        var dataService = Substitute.For<IDashboardDataService>();
        var alertService = Substitute.For<IAlertService>();
        var hubContext = Substitute.For<IHubContext<DashboardHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        return (new BackupStatusController(dataService, alertService, hubContext), dataService, alertService);
    }

    [Fact]
    public async Task PostBackupStatus_UpsertsAndReturnsOk()
    {
        var (controller, dataService, _) = CreateController();
        var json = JsonSerializer.Serialize(new { customerId = "cust1", machineName = "PC1" });
        var body = JsonDocument.Parse(json).RootElement;

        dataService.UpsertBackupStatusAsync("cust1", "PC1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BackupStatusRecord { Id = 1, CustomerId = "cust1" });

        var result = await controller.PostBackupStatus(body, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await dataService.Received(1).UpsertBackupStatusAsync("cust1", "PC1",
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostBackupStatus_ReturnsBadRequest_WhenNoCustomerId()
    {
        var (controller, _, _) = CreateController();
        var json = JsonSerializer.Serialize(new { machineName = "PC1" });
        var body = JsonDocument.Parse(json).RootElement;

        var result = await controller.PostBackupStatus(body, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostBackupStatus_EvaluatesAlerts()
    {
        var (controller, dataService, alertService) = CreateController();
        var json = JsonSerializer.Serialize(new { customerId = "c2", machineName = "PC2" });
        var body = JsonDocument.Parse(json).RootElement;

        var record = new BackupStatusRecord { Id = 1, CustomerId = "c2" };
        dataService.UpsertBackupStatusAsync("c2", "PC2", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await controller.PostBackupStatus(body, CancellationToken.None);

        await alertService.Received(1).EvaluateBackupStatusAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListBackupStatuses_ReturnsAll()
    {
        var (controller, dataService, _) = CreateController();
        dataService.ListBackupStatusesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BackupStatusRecord>
            {
                new() { CustomerId = "c1" },
                new() { CustomerId = "c2" }
            });

        var result = await controller.ListBackupStatuses(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostBackupStatus_HandlesPascalCaseCustomerId()
    {
        var (controller, dataService, _) = CreateController();
        var json = "{\"CustomerId\":\"cust3\",\"MachineName\":\"PC3\"}";
        var body = JsonDocument.Parse(json).RootElement;

        dataService.UpsertBackupStatusAsync("cust3", "PC3", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BackupStatusRecord { Id = 1, CustomerId = "cust3" });

        var result = await controller.PostBackupStatus(body, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
