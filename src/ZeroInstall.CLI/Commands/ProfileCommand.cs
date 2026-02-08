using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim profile list|show|save|delete — Manage migration profiles.
/// </summary>
internal static class ProfileCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("profile", "Manage migration profile templates")
        {
            CreateListCommand(verboseOption, jsonOption),
            CreateShowCommand(verboseOption, jsonOption),
            CreateSaveCommand(verboseOption),
            CreateDeleteCommand(verboseOption)
        };

        return command;
    }

    private static Command CreateListCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var sourceOption = new Option<string>("--source")
        {
            Description = "Profile source: local, nas, or all",
            DefaultValueFactory = _ => "all"
        };

        var listCommand = new Command("list", "List available migration profiles")
        {
            sourceOption
        };

        listCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var source = parseResult.GetValue(sourceOption) ?? "all";

            using var host = CliHost.BuildHost(verbose);
            var manager = host.Services.GetRequiredService<IProfileManager>();

            try
            {
                var profiles = new List<MigrationProfile>();

                if (source is "local" or "all")
                    profiles.AddRange(await manager.ListLocalProfilesAsync(ct));

                if (source is "nas" or "all")
                    profiles.AddRange(await manager.ListNasProfilesAsync(ct));

                var distinct = profiles
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(p => p.Name)
                    .ToList();

                OutputFormatter.WriteProfileList(distinct, json);
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
        var nameArg = new Argument<string>("name")
        {
            Description = "Profile name to display"
        };

        var sourceOption = new Option<string>("--source")
        {
            Description = "Profile source: local or nas",
            DefaultValueFactory = _ => "local"
        };

        var showCommand = new Command("show", "Show details of a migration profile")
        {
            nameArg, sourceOption
        };

        showCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(nameArg)!;
            var source = parseResult.GetValue(sourceOption) ?? "local";

            using var host = CliHost.BuildHost(verbose);
            var manager = host.Services.GetRequiredService<IProfileManager>();

            try
            {
                var profile = source == "nas"
                    ? await manager.LoadNasProfileAsync(name, ct)
                    : await manager.LoadLocalProfileAsync(name, ct);

                if (profile is null)
                {
                    Console.Error.WriteLine($"Profile '{name}' not found.");
                    return 1;
                }

                OutputFormatter.WriteProfileDetail(profile, json);
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

    private static Command CreateSaveCommand(Option<bool> verboseOption)
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Profile name"
        };

        var descOption = new Option<string?>("--description")
        {
            Description = "Profile description"
        };

        var authorOption = new Option<string?>("--author")
        {
            Description = "Profile author"
        };

        var saveCommand = new Command("save", "Save a new migration profile template")
        {
            nameArg, descOption, authorOption
        };

        saveCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var name = parseResult.GetValue(nameArg)!;
            var description = parseResult.GetValue(descOption);
            var author = parseResult.GetValue(authorOption);

            using var host = CliHost.BuildHost(verbose);
            var manager = host.Services.GetRequiredService<IProfileManager>();

            try
            {
                var profile = new MigrationProfile
                {
                    Name = name,
                    Description = description ?? "",
                    Author = author ?? Environment.UserName,
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow
                };

                await manager.SaveLocalProfileAsync(profile, ct);
                Console.WriteLine($"Profile '{name}' saved.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return saveCommand;
    }

    private static Command CreateDeleteCommand(Option<bool> verboseOption)
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Profile name to delete"
        };

        var deleteCommand = new Command("delete", "Delete a local migration profile")
        {
            nameArg
        };

        deleteCommand.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var name = parseResult.GetValue(nameArg)!;

            using var host = CliHost.BuildHost(verbose);
            var manager = host.Services.GetRequiredService<IProfileManager>();

            try
            {
                await manager.DeleteLocalProfileAsync(name, ct);
                Console.WriteLine($"Profile '{name}' deleted.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return deleteCommand;
    }
}
