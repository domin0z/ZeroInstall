using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZeroInstall.Core.Models;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IDashboardDataService _dataService;

    public ReportsController(IDashboardDataService dataService)
    {
        _dataService = dataService;
    }

    [HttpPost]
    public async Task<IActionResult> PostReport([FromBody] JobReport report, CancellationToken ct)
    {
        var record = await _dataService.UpsertReportAsync(report, ct);
        return Ok(new { record.Id, record.ReportId, record.JobId });
    }

    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetReportByJobId(string jobId, CancellationToken ct)
    {
        var record = await _dataService.GetReportByJobIdAsync(jobId, ct);
        if (record is null)
            return NotFound();
        return Ok(record);
    }
}
