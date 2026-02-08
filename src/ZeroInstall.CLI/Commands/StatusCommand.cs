using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim status [--json] [--verbose]
/// Shows the status of the most recent migration job.
/// </summary>
internal static class StatusCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("status", "Show the status of the most recent migration job");

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();

            try
            {
                var jobs = await jobLogger.ListJobsAsync(ct: ct);

                if (jobs.Count == 0)
                {
                    if (json)
                        Console.WriteLine("null");
                    else
                        Console.WriteLine("No migration jobs found.");
                    return 0;
                }

                var latest = jobs[0];
                OutputFormatter.WriteJobDetail(latest, json);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
