using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim restore --input path [--user-map src:dest ...] [--create-users] [--json] [--verbose]
/// Restores captured data to the destination machine.
/// </summary>
internal static class RestoreCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var inputOption = new Option<string>("--input", "-i")
        {
            Description = "Input directory containing captured data",
            Required = true
        };

        var userMapOption = new Option<string[]>("--user-map")
        {
            Description = "User mapping in source:dest format (repeatable)",
            AllowMultipleArgumentsPerToken = true
        };

        var createUsersOption = new Option<bool>("--create-users")
        {
            Description = "Create destination user accounts if they don't exist"
        };

        var command = new Command("restore", "Restore captured data to this machine")
        {
            inputOption, userMapOption, createUsersOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var input = parseResult.GetValue(inputOption)!;
            var userMaps = parseResult.GetValue(userMapOption) ?? Array.Empty<string>();
            var createUsers = parseResult.GetValue(createUsersOption);

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();
            var progress = new ConsoleProgressReporter();

            try
            {
                if (!Directory.Exists(input))
                {
                    Console.Error.WriteLine($"Error: Input path does not exist: {input}");
                    return 1;
                }

                var mappings = ParseUserMappings(userMaps, createUsers);

                var job = new MigrationJob
                {
                    DestinationHostname = Environment.MachineName,
                    DestinationOsVersion = Environment.OSVersion.ToString(),
                    Status = JobStatus.InProgress,
                    StartedUtc = DateTime.UtcNow,
                    UserMappings = mappings
                };
                await jobLogger.CreateJobAsync(job, ct);
                Console.Error.WriteLine($"Restore job {job.JobId} started.");

                var packMigrator = host.Services.GetRequiredService<IPackageMigrator>();
                Console.Error.WriteLine("Restoring package-based items...");
                await packMigrator.RestoreAsync(input, mappings, progress, ct);
                progress.Complete();

                var regMigrator = host.Services.GetRequiredService<IRegistryMigrator>();
                Console.Error.WriteLine("Restoring registry+file items...");
                await regMigrator.RestoreAsync(input, mappings, progress, ct);
                progress.Complete();

                job.Status = JobStatus.Completed;
                job.CompletedUtc = DateTime.UtcNow;
                await jobLogger.UpdateJobAsync(job, ct);

                if (json)
                    OutputFormatter.WriteJobDetail(job, true);
                else
                    Console.WriteLine($"Restore complete. Job ID: {job.JobId}");

                return 0;
            }
            catch (Exception ex)
            {
                progress.Complete();
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    private static List<UserMapping> ParseUserMappings(string[] maps, bool createIfMissing)
    {
        var mappings = new List<UserMapping>();

        foreach (var map in maps)
        {
            var parts = map.Split(':', 2);
            if (parts.Length != 2)
            {
                Console.Error.WriteLine($"Warning: Invalid user mapping format '{map}', expected 'source:dest'");
                continue;
            }

            mappings.Add(new UserMapping
            {
                SourceUser = new UserProfile { Username = parts[0].Trim() },
                DestinationUsername = parts[1].Trim(),
                DestinationProfilePath = Path.Combine(@"C:\Users", parts[1].Trim()),
                CreateIfMissing = createIfMissing
            });
        }

        return mappings;
    }
}
