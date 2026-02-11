using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZeroInstall.Dashboard.Hubs;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Api;

[ApiController]
[Route("api/backup-status")]
[Authorize]
public class BackupStatusController : ControllerBase
{
    private readonly IDashboardDataService _dataService;
    private readonly IAlertService _alertService;
    private readonly IHubContext<DashboardHub> _hubContext;

    public BackupStatusController(
        IDashboardDataService dataService,
        IAlertService alertService,
        IHubContext<DashboardHub> hubContext)
    {
        _dataService = dataService;
        _alertService = alertService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> PostBackupStatus([FromBody] JsonElement body, CancellationToken ct)
    {
        var rawJson = body.GetRawText();

        var customerId = body.TryGetProperty("customerId", out var cid) ? cid.GetString()
            : body.TryGetProperty("CustomerId", out var cid2) ? cid2.GetString()
            : null;

        var machineName = body.TryGetProperty("machineName", out var mn) ? mn.GetString()
            : body.TryGetProperty("MachineName", out var mn2) ? mn2.GetString()
            : null;

        if (string.IsNullOrEmpty(customerId))
            return BadRequest("Missing customerId");

        var record = await _dataService.UpsertBackupStatusAsync(customerId, machineName ?? "", rawJson, ct);
        await _alertService.EvaluateBackupStatusAsync(record, ct);
        await _hubContext.Clients.All.SendAsync("BackupStatusChanged", customerId, ct);

        return Ok(new { record.Id, record.CustomerId });
    }

    [HttpGet]
    public async Task<IActionResult> ListBackupStatuses(CancellationToken ct)
    {
        var statuses = await _dataService.ListBackupStatusesAsync(ct);
        return Ok(statuses);
    }
}
