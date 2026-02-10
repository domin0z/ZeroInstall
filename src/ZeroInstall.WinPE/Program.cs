using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.Core.Models;
using ZeroInstall.WinPE.Commands;
using ZeroInstall.WinPE.Infrastructure;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE;

/// <summary>
/// Entry point for the ZeroInstall WinPE Restore Tool (zim-winpe).
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose logging output"
        };

        var imageOption = new Option<string?>("--image")
        {
            Description = "Path to disk image file (.img/.raw/.vhdx) for headless restore"
        };

        var targetOption = new Option<string?>("--target")
        {
            Description = "Target volume path (e.g., D:\\) for headless restore"
        };

        var driverPathOption = new Option<string?>("--driver-path")
        {
            Description = "Path to driver directory for post-restore injection"
        };

        var skipVerifyOption = new Option<bool>("--skip-verify")
        {
            Description = "Skip image integrity verification"
        };

        var noConfirmOption = new Option<bool>("--no-confirm")
        {
            Description = "Skip confirmation prompts (for scripted use)"
        };

        var rootCommand = new RootCommand("ZeroInstall WinPE Restore Tool (zim-winpe) — Restore disk images with driver injection")
        {
            verboseOption,
            imageOption,
            targetOption,
            driverPathOption,
            skipVerifyOption,
            noConfirmOption
        };

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var imagePath = parseResult.GetValue(imageOption);
            var targetPath = parseResult.GetValue(targetOption);
            var driverPath = parseResult.GetValue(driverPathOption);
            var skipVerify = parseResult.GetValue(skipVerifyOption);
            var noConfirm = parseResult.GetValue(noConfirmOption);

            using var host = WinPeHost.BuildHost(verbose);

            // If both --image and --target provided, run headless
            if (!string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(targetPath))
            {
                return await RunHeadlessAsync(host.Services, imagePath, targetPath,
                    driverPath, skipVerify, noConfirm, ct);
            }

            // Otherwise, run interactive
            return await RestoreInteractiveCommand.RunAsync(host, ct);
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunHeadlessAsync(
        IServiceProvider services,
        string imagePath,
        string targetPath,
        string? driverPath,
        bool skipVerify,
        bool noConfirm,
        CancellationToken ct)
    {
        WinPeConsoleUI.WriteHeader();

        if (!File.Exists(imagePath))
        {
            WinPeConsoleUI.WriteError($"Image file not found: {imagePath}");
            return 1;
        }

        if (!noConfirm)
        {
            Console.WriteLine($"  Image:  {imagePath}");
            Console.WriteLine($"  Target: {targetPath}");
            WinPeConsoleUI.WriteWarning("WARNING: This will OVERWRITE all data on the target volume!");

            if (!WinPeConsoleUI.PromptYesNo("Proceed with restore?"))
            {
                Console.WriteLine("  Restore cancelled.");
                return 1;
            }
        }

        var orchestrator = services.GetRequiredService<RestoreOrchestrator>();
        var progress = new Progress<TransferProgress>(WinPeConsoleUI.WriteProgress);

        var options = new RestoreOptions
        {
            SkipVerify = skipVerify,
            DriverPath = driverPath,
            Recurse = true
        };

        var result = await orchestrator.RunRestoreAsync(imagePath, targetPath, options, progress, ct);

        Console.WriteLine();
        if (result.Success)
        {
            WinPeConsoleUI.WriteSuccess($"Restore completed in {WinPeConsoleUI.FormatTimeSpan(result.Duration)}");

            if (result.DriverResult != null)
            {
                if (result.DriverResult.Success)
                    WinPeConsoleUI.WriteSuccess($"Drivers injected: {result.DriverResult.AddedCount} driver(s) added");
                else
                    WinPeConsoleUI.WriteWarning(
                        $"Driver injection: {result.DriverResult.AddedCount} added, {result.DriverResult.FailedCount} failed");
            }

            return 0;
        }

        WinPeConsoleUI.WriteError($"Restore failed: {result.Error}");
        return 1;
    }
}
