using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim firmware status
/// zim firmware backup-bcd &lt;path&gt;
/// zim firmware restore-bcd &lt;path&gt;
/// zim firmware list-boot-entries
/// </summary>
internal static class FirmwareCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("firmware", "Query firmware information and manage BCD boot configuration");

        command.Add(CreateStatusCommand(verboseOption, jsonOption));
        command.Add(CreateBackupBcdCommand(verboseOption, jsonOption));
        command.Add(CreateRestoreBcdCommand(verboseOption, jsonOption));
        command.Add(CreateListBootEntriesCommand(verboseOption, jsonOption));

        return command;
    }

    private static Command CreateStatusCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("status", "Show firmware information (BIOS, Secure Boot, TPM, system)");

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);

            using var host = CliHost.BuildHost(verbose);
            var firmware = host.Services.GetRequiredService<IFirmwareService>();

            try
            {
                var info = await firmware.GetFirmwareInfoAsync(ct);
                OutputFormatter.WriteFirmwareInfo(info, json);
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

    private static Command CreateBackupBcdCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Destination file path for the BCD backup"
        };

        var command = new Command("backup-bcd", "Export the BCD store to a backup file")
        {
            pathArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var path = parseResult.GetValue(pathArg)!;

            using var host = CliHost.BuildHost(verbose);
            var firmware = host.Services.GetRequiredService<IFirmwareService>();

            try
            {
                var success = await firmware.ExportBcdAsync(path, ct);

                if (json)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { path, success }));
                }
                else
                {
                    Console.WriteLine(success
                        ? $"BCD store exported to: {path}"
                        : $"Failed to export BCD store to: {path}");
                }

                return success ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateRestoreBcdCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Path to the BCD backup file to restore"
        };

        var command = new Command("restore-bcd", "Import a BCD store from a backup file")
        {
            pathArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var path = parseResult.GetValue(pathArg)!;

            using var host = CliHost.BuildHost(verbose);
            var firmware = host.Services.GetRequiredService<IFirmwareService>();

            try
            {
                if (!json)
                {
                    Console.WriteLine("WARNING: Importing a BCD store will overwrite the current boot configuration.");
                    Console.WriteLine("         This should only be done on a destination machine during migration.");
                    Console.WriteLine();
                }

                var success = await firmware.ImportBcdAsync(path, ct);

                if (json)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { path, success }));
                }
                else
                {
                    Console.WriteLine(success
                        ? $"BCD store imported from: {path}"
                        : $"Failed to import BCD store from: {path}");
                }

                return success ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateListBootEntriesCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("list-boot-entries", "List all BCD boot entries");

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);

            using var host = CliHost.BuildHost(verbose);
            var firmware = host.Services.GetRequiredService<IFirmwareService>();

            try
            {
                var entries = await firmware.GetBootEntriesAsync(ct);
                OutputFormatter.WriteBootEntries(entries, json);
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
