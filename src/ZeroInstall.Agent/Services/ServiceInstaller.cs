using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Installs/uninstalls the agent as a Windows Service via sc.exe.
/// </summary>
internal class ServiceInstaller
{
    private const string ServiceName = "ZeroInstallAgent";
    private const string DisplayName = "ZeroInstall Transfer Agent";

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<ServiceInstaller> _logger;

    public ServiceInstaller(IProcessRunner processRunner, ILogger<ServiceInstaller> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<bool> InstallAsync(string exePath, string key, int port, CancellationToken ct = default)
    {
        var binPath = $"\"{exePath}\" --role source --key \"{key}\" --port {port} --mode service --dir \"{Path.GetDirectoryName(exePath)}\"";

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
        // Stop first, then delete
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
