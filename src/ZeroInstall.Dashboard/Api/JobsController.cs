using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Api;

[ApiController]
[Route("api/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IDashboardDataService _dataService;
    private readonly IAlertService _alertService;
    private readonly IHubContext<DashboardHub> _hubContext;

    public JobsController(
        IDashboardDataService dataService,
        IAlertService alertService,
        IHubContext<DashboardHub> hubContext)
    {
        _dataService = dataService;
        _alertService = alertService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> PostJob([FromBody] MigrationJob job, CancellationToken ct)
    {
        var record = await _dataService.UpsertJobAsync(job, ct);
        await _alertService.EvaluateJobAsync(record, ct);
        await _hubContext.Clients.All.SendAsync("JobUpdated", record.JobId, record.Status, ct);

        return Ok(new { record.Id, record.JobId, record.Status });
    }

    [HttpGet]
    public async Task<IActionResult> ListJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? technician = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var jobs = await _dataService.ListJobsAsync(status, technician, skip, take, ct);
        var total = await _dataService.GetJobCountAsync(status, ct);
        return Ok(new { total, jobs });
    }

    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken ct)
    {
        var record = await _dataService.GetJobAsync(jobId, ct);
        if (record is null)
            return NotFound();
        return Ok(record);
    }
}
