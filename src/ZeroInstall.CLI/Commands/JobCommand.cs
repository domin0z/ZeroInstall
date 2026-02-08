using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim job list|show|export — Manage migration jobs and reports.
/// </summary>
internal static class JobCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("job", "View and manage migration jobs")
        {
            CreateListCommand(verboseOption, jsonOption),
            CreateShowCommand(verboseOption, jsonOption),
            CreateExportCommand(verboseOption, jsonOption)
        };

        return command;
    }

    private static Command CreateListCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var statusOption = new Option<string>("--status")
        {
            Description = "Filter by status: pending, inprogress, completed, failed, all",
            DefaultValueFactory = _ => "all"
        };

        var listCommand = new Command("list", "List migration jobs")
        {
            statusOption
        };

        listCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var statusStr = parseResult.GetValue(statusOption) ?? "all";

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();

            try
            {
                JobStatus? filter = ParseStatus(statusStr);
                var jobs = await jobLogger.ListJobsAsync(filter, ct);

                OutputFormatter.WriteJobList(jobs, json);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return listCommand;
    }

    private static Command CreateShowCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var idArg = new Argument<string>("id")
        {
            Description = "Job ID to display"
        };

        var showCommand = new Command("show", "Show details of a migration job")
        {
            idArg
        };

        showCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(idArg)!;

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();

            try
            {
                var job = await jobLogger.GetJobAsync(id, ct);

                if (job is null)
                {
                    Console.Error.WriteLine($"Job '{id}' not found.");
                    return 1;
                }

                OutputFormatter.WriteJobDetail(job, json);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return showCommand;
    }

    private static Command CreateExportCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var idArg = new Argument<string>("id")
        {
            Description = "Job ID to export report for"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output file path (defaults to {id}-report.json)"
        };

        var exportCommand = new Command("export", "Generate and export a job report")
        {
            idArg, outputOption
        };

        exportCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(idArg)!;
            var output = parseResult.GetValue(outputOption);

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();

            try
            {
                var report = await jobLogger.GenerateReportAsync(id, ct);
                var outputPath = output ?? $"{id}-report.json";

                await jobLogger.ExportReportAsync(report, outputPath, ct);

                if (json)
                    OutputFormatter.WriteReport(report, true);
                else
                    Console.WriteLine($"Report exported to {outputPath}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return exportCommand;
    }

    private static JobStatus? ParseStatus(string status) => status.ToLowerInvariant() switch
    {
        "pending" => JobStatus.Pending,
        "inprogress" => JobStatus.InProgress,
        "completed" => JobStatus.Completed,
        "failed" => JobStatus.Failed,
        _ => null
    };
}
