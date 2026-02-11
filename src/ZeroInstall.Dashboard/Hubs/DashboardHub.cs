using Microsoft.AspNetCore.SignalR;

namespace ZeroInstall.Dashboard.Hubs;

public class DashboardHub : Hub
{
    // Server-to-client methods (clients subscribe):
    // - JobUpdated(string jobId, string status)
    // - BackupStatusChanged(string customerId)
    // - AlertCreated(int alertId, string alertType, string message)
    // - AlertDismissed(int alertId)
    // - StatsChanged(DashboardStats stats)
}
