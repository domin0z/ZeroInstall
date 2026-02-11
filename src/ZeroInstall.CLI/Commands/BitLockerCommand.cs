using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim bitlocker status [volume]
/// zim bitlocker unlock &lt;volume&gt; --recovery-password &lt;key&gt;
/// </summary>
internal static class BitLockerCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("bitlocker", "Query and manage BitLocker encryption status");

        command.Add(CreateStatusCommand(verboseOption, jsonOption));
        command.Add(CreateUnlockCommand(verboseOption, jsonOption));

        return command;
    }

    private static Command CreateStatusCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var volumeArg = new Argument<string?>("volume")
        {
            Description = "Volume to check (e.g., C:). If omitted, shows all volumes.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("status", "Show BitLocker status for volumes")
        {
            volumeArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var volume = parseResult.GetValue(volumeArg);

            using var host = CliHost.BuildHost(verbose);
            var bitLocker = host.Services.GetRequiredService<IBitLockerService>();

            try
            {
                if (!string.IsNullOrEmpty(volume))
                {
                    var status = await bitLocker.GetStatusAsync(volume, ct);
                    OutputFormatter.WriteBitLockerStatus([status], json);
                }
                else
                {
                    // Enumerate all volumes and show their BitLocker status
                    var diskEnum = host.Services.GetRequiredService<DiskEnumerationService>();
                    var volumes = await diskEnum.GetVolumesAsync(ct);

                    var statuses = new List<Core.Models.BitLockerStatus>();
                    foreach (var v in volumes.Where(v => !string.IsNullOrEmpty(v.DriveLetter)))
                    {
                        var status = await bitLocker.GetStatusAsync(v.DriveLetter + ":", ct);
                        statuses.Add(status);
                    }

                    OutputFormatter.WriteBitLockerStatus(statuses, json);
                }

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

    private static Command CreateUnlockCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var volumeArg = new Argument<string>("volume")
        {
            Description = "Volume to unlock (e.g., D:)"
        };

        var passwordOption = new Option<string?>("--recovery-password")
        {
            Description = "48-digit BitLocker recovery password"
        };

        var keyFileOption = new Option<string?>("--recovery-key")
        {
            Description = "Path to .bek recovery key file"
        };

        var command = new Command("unlock", "Unlock a BitLocker-locked volume")
        {
            volumeArg,
            passwordOption,
            keyFileOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var volume = parseResult.GetValue(volumeArg)!;
            var password = parseResult.GetValue(passwordOption);
            var keyFile = parseResult.GetValue(keyFileOption);

            if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(keyFile))
            {
                Console.Error.WriteLine("Error: Provide either --recovery-password or --recovery-key");
                return 1;
            }

            using var host = CliHost.BuildHost(verbose);
            var bitLocker = host.Services.GetRequiredService<IBitLockerService>();

            try
            {
                var success = await bitLocker.UnlockVolumeAsync(volume, password, keyFile, ct);

                if (json)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { volume, success }));
                }
                else
                {
                    Console.WriteLine(success
                        ? $"Volume {volume} unlocked successfully."
                        : $"Failed to unlock volume {volume}. Check the recovery password/key.");
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
}
