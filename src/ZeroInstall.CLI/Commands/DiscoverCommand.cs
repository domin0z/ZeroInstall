using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim discover [--type apps|profiles|settings|all] [--json] [--verbose]
/// Scans the machine and outputs discovered items.
/// </summary>
internal static class DiscoverCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var typeOption = new Option<string>("--type")
        {
            Description = "Type of items to discover: apps, profiles, settings, or all",
            DefaultValueFactory = _ => "all"
        };

        var command = new Command("discover", "Scan this machine for applications, profiles, and settings")
        {
            typeOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var type = parseResult.GetValue(typeOption) ?? "all";

            using var host = CliHost.BuildHost(verbose);
            var discovery = host.Services.GetRequiredService<IDiscoveryService>();
            var progress = new ConsoleProgressReporter();

            try
            {
                IReadOnlyList<MigrationItem> items;

                switch (type.ToLowerInvariant())
                {
                    case "apps":
                    case "applications":
                        var apps = await discovery.DiscoverApplicationsAsync(ct);
                        items = apps.Select(a => new MigrationItem
                        {
                            DisplayName = a.Name,
                            ItemType = MigrationItemType.Application,
                            RecommendedTier = a.RecommendedTier,
                            EstimatedSizeBytes = a.EstimatedSizeBytes,
                            SourceData = a
                        }).ToList().AsReadOnly();
                        break;

                    case "profiles":
                        var profiles = await discovery.DiscoverUserProfilesAsync(ct);
                        items = profiles.Select(p => new MigrationItem
                        {
                            DisplayName = p.Username,
                            ItemType = MigrationItemType.UserProfile,
                            RecommendedTier = MigrationTier.Package,
                            EstimatedSizeBytes = p.EstimatedSizeBytes,
                            SourceData = p
                        }).ToList().AsReadOnly();
                        break;

                    case "settings":
                        var settings = await discovery.DiscoverSystemSettingsAsync(ct);
                        items = settings.Select(s => new MigrationItem
                        {
                            DisplayName = s.Name,
                            ItemType = MigrationItemType.SystemSetting,
                            RecommendedTier = MigrationTier.Package,
                            SourceData = s
                        }).ToList().AsReadOnly();
                        break;

                    default:
                        items = await discovery.DiscoverAllAsync(progress, ct);
                        progress.Complete();
                        break;
                }

                OutputFormatter.WriteDiscoveryResults(items, json);
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
}
