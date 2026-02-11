using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI;

/// <summary>
/// Entry point for the ZeroInstall Migrator CLI (zim).
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Global options
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Enable verbose/debug output"
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output results as JSON"
        };

        var rootCommand = new RootCommand("ZeroInstall Migrator (zim) — Portable PC migration tool")
        {
            verboseOption,
            jsonOption,
            DiscoverCommand.Create(verboseOption, jsonOption),
            CaptureCommand.Create(verboseOption, jsonOption),
            RestoreCommand.Create(verboseOption, jsonOption),
            StatusCommand.Create(verboseOption, jsonOption),
            ProfileCommand.Create(verboseOption, jsonOption),
            JobCommand.Create(verboseOption, jsonOption),
            BitLockerCommand.Create(verboseOption, jsonOption)
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
