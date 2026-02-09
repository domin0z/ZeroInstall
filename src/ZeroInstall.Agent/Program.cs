using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroInstall.Agent.Infrastructure;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Agent;

/// <summary>
/// Entry point for the ZeroInstall Transfer Agent (zim-agent).
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var roleOption = new Option<AgentRole>("--role")
        {
            Description = "Agent role: source (sender) or destination (receiver)",
            Arity = ArgumentArity.ExactlyOne
        };

        var portOption = new Option<int>("--port")
        {
            Description = "TCP port for the transfer connection",
            DefaultValueFactory = _ => 19850
        };

        var keyOption = new Option<string>("--key")
        {
            Description = "Shared authentication key (must match on both sides)",
            Arity = ArgumentArity.ExactlyOne
        };

        var modeOption = new Option<AgentMode>("--mode")
        {
            Description = "Run mode: portable (console) or service",
            DefaultValueFactory = _ => AgentMode.Portable
        };

        var dirOption = new Option<string>("--dir")
        {
            Description = "Directory path for captured data (source) or output (destination)",
            Arity = ArgumentArity.ExactlyOne
        };

        var peerOption = new Option<string?>("--peer")
        {
            Description = "Direct peer address (skips UDP discovery)"
        };

        // Root command — run agent
        var rootCommand = new RootCommand("ZeroInstall Transfer Agent (zim-agent) — Portable file transfer over TCP")
        {
            roleOption,
            portOption,
            keyOption,
            modeOption,
            dirOption,
            peerOption
        };

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var options = new AgentOptions
            {
                Role = parseResult.GetValue(roleOption),
                Port = parseResult.GetValue(portOption),
                SharedKey = parseResult.GetValue(keyOption)!,
                Mode = parseResult.GetValue(modeOption),
                DirectoryPath = parseResult.GetValue(dirOption)!,
                PeerAddress = parseResult.GetValue(peerOption)
            };

            using var host = AgentHost.BuildHost(options);
            await host.RunAsync(ct);
            return 0;
        });

        // install subcommand
        var installKeyOption = new Option<string>("--key")
        {
            Description = "Shared authentication key for the service",
            Arity = ArgumentArity.ExactlyOne
        };

        var installPortOption = new Option<int>("--port")
        {
            Description = "TCP port for the service",
            DefaultValueFactory = _ => 19850
        };

        var installCommand = new Command("install", "Install the agent as a Windows Service (requires admin)")
        {
            installKeyOption,
            installPortOption
        };

        installCommand.SetAction(async (parseResult, ct) =>
        {
            var key = parseResult.GetValue(installKeyOption)!;
            var port = parseResult.GetValue(installPortOption);
            var exePath = Environment.ProcessPath ?? "zim-agent.exe";

            var processRunner = new WindowsProcessRunner();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceInstaller>.Instance;
            var installer = new ServiceInstaller(processRunner, logger);

            var success = await installer.InstallAsync(exePath, key, port, ct);
            if (success)
            {
                Console.WriteLine("Service installed successfully.");
                Console.WriteLine("Start it with: sc.exe start ZeroInstallAgent");
            }
            else
            {
                Console.Error.WriteLine("Failed to install service. Are you running as administrator?");
            }
            return success ? 0 : 1;
        });

        // uninstall subcommand
        var uninstallCommand = new Command("uninstall", "Uninstall the agent Windows Service (requires admin)");

        uninstallCommand.SetAction(async (parseResult, ct) =>
        {
            var processRunner = new WindowsProcessRunner();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceInstaller>.Instance;
            var installer = new ServiceInstaller(processRunner, logger);

            var success = await installer.UninstallAsync(ct);
            Console.WriteLine(success ? "Service uninstalled successfully." : "Failed to uninstall service.");
            return success ? 0 : 1;
        });

        rootCommand.Add(installCommand);
        rootCommand.Add(uninstallCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
