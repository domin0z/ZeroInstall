using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Installs/uninstalls the backup agent as a Windows Service via sc.exe.
/// </summary>
internal class BackupServiceInstaller
{
    private const string ServiceName = "ZeroInstallBackup";
    private const string DisplayName = "ZeroInstall Backup Agent";

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<BackupServiceInstaller> _logger;

    public BackupServiceInstaller(IProcessRunner processRunner, ILogger<BackupServiceInstaller> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<bool> InstallAsync(string exePath, string configPath, CancellationToken ct = default)
    {
        var binPath = $"\"{exePath}\" --service --config \"{configPath}\"";

        var result = await _processRunner.RunAsync("sc.exe",
            $"create {ServiceName} binPath= \"{binPath}\" DisplayName= \"{DisplayName}\" start= auto", ct);

        if (result.Success)
            _logger.LogInformation("Service '{ServiceName}' installed successfully", ServiceName);
        else
            _logger.LogError("Failed to install service: {Error}", result.StandardError);

        return result.Success;
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        await _processRunner.RunAsync("sc.exe", $"stop {ServiceName}", ct);
        var result = await _processRunner.RunAsync("sc.exe", $"delete {ServiceName}", ct);

        if (result.Success)
            _logger.LogInformation("Service '{ServiceName}' uninstalled successfully", ServiceName);
        else
            _logger.LogError("Failed to uninstall service: {Error}", result.StandardError);

        return result.Success;
    }

    public async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        var result = await _processRunner.RunAsync("sc.exe", $"query {ServiceName}", ct);
        return result.Success && result.StandardOutput.Contains(ServiceName);
    }
}
