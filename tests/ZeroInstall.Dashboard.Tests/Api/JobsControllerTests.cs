using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Api;
using ZeroInstall.Dashboard.Data.Entities;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Api;

public class JobsControllerTests
{
    private static (JobsController, IDashboardDataService, IAlertService) CreateController()
    {
        var dataService = Substitute.For<IDashboardDataService>();
        var alertService = Substitute.For<IAlertService>();
        var hubContext = Substitute.For<IHubContext<DashboardHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        return (new JobsController(dataService, alertService, hubContext), dataService, alertService);
    }

    [Fact]
    public async Task PostJob_CreatesJobAndReturnsOk()
    {
        var (controller, dataService, _) = CreateController();
        var job = new MigrationJob { JobId = "post1", Status = JobStatus.Completed };
        dataService.UpsertJobAsync(job, Arg.Any<CancellationToken>())
            .Returns(new JobRecord { Id = 1, JobId = "post1", Status = "Completed" });

        var result = await controller.PostJob(job, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await dataService.Received(1).UpsertJobAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostJob_EvaluatesAlerts()
    {
        var (controller, dataService, alertService) = CreateController();
        var job = new MigrationJob { JobId = "alert1", Status = JobStatus.Failed };
        var record = new JobRecord { Id = 1, JobId = "alert1", Status = "Failed" };
        dataService.UpsertJobAsync(job, Arg.Any<CancellationToken>()).Returns(record);

        await controller.PostJob(job, CancellationToken.None);

        await alertService.Received(1).EvaluateJobAsync(record, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListJobs_ReturnsJobsWithTotal()
    {
        var (controller, dataService, _) = CreateController();
        dataService.ListJobsAsync(null, null, 0, 50, Arg.Any<CancellationToken>())
            .Returns(new List<JobRecord> { new() { JobId = "l1" } });
        dataService.GetJobCountAsync(null, Arg.Any<CancellationToken>()).Returns(1);

        var result = await controller.ListJobs();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetJob_ReturnsNotFound_WhenMissing()
    {
        var (controller, dataService, _) = CreateController();
        dataService.GetJobAsync("missing", Arg.Any<CancellationToken>()).Returns((JobRecord?)null);

        var result = await controller.GetJob("missing", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetJob_ReturnsOk_WhenFound()
    {
        var (controller, dataService, _) = CreateController();
        dataService.GetJobAsync("found1", Arg.Any<CancellationToken>())
            .Returns(new JobRecord { JobId = "found1", Status = "Completed" });

        var result = await controller.GetJob("found1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
