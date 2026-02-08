using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim capture --output path [--tier package|regfile|clone|auto] [--profile name]
///             [--all] [--volume letter] [--format img|raw|vhdx] [--json] [--verbose]
/// Captures data from the source machine.
/// </summary>
internal static class CaptureCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var outputOption = new Option<string>("--output", "-o")
        {
            Description = "Output directory for captured data",
            Required = true
        };

        var tierOption = new Option<string>("--tier")
        {
            Description = "Migration tier: package, regfile, clone, or auto",
            DefaultValueFactory = _ => "auto"
        };

        var profileOption = new Option<string?>("--profile")
        {
            Description = "Migration profile name to apply"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Select all discovered items (default if no profile)"
        };

        var volumeOption = new Option<string?>("--volume")
        {
            Description = "Volume letter for full clone (e.g., C)"
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Disk image format for clone tier: img, raw, or vhdx",
            DefaultValueFactory = _ => "vhdx"
        };

        var command = new Command("capture", "Capture data from this machine for migration")
        {
            outputOption, tierOption, profileOption, allOption, volumeOption, formatOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var output = parseResult.GetValue(outputOption)!;
            var tier = parseResult.GetValue(tierOption) ?? "auto";
            var profileName = parseResult.GetValue(profileOption);
            var all = parseResult.GetValue(allOption);
            var volume = parseResult.GetValue(volumeOption);
            var format = parseResult.GetValue(formatOption) ?? "vhdx";

            using var host = CliHost.BuildHost(verbose);
            var discovery = host.Services.GetRequiredService<IDiscoveryService>();
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();
            var profileManager = host.Services.GetRequiredService<IProfileManager>();
            var progress = new ConsoleProgressReporter();

            try
            {
                // Discover items
                Console.Error.WriteLine("Discovering items...");
                var items = await discovery.DiscoverAllAsync(progress, ct);
                progress.Complete();
                Console.Error.WriteLine($"Found {items.Count} items.");

                // Apply profile or select all
                var selectedItems = items.ToList();
                if (!string.IsNullOrEmpty(profileName))
                {
                    var profile = await profileManager.LoadLocalProfileAsync(profileName, ct)
                        ?? await profileManager.LoadNasProfileAsync(profileName, ct);

                    if (profile is null)
                    {
                        Console.Error.WriteLine($"Error: Profile '{profileName}' not found.");
                        return 1;
                    }

                    ApplyProfile(selectedItems, profile);
                    Console.Error.WriteLine($"Applied profile '{profile.Name}'.");
                }
                else if (!all)
                {
                    foreach (var item in selectedItems)
                        item.IsSelected = true;
                }

                // Create job
                var job = new MigrationJob
                {
                    SourceHostname = Environment.MachineName,
                    SourceOsVersion = Environment.OSVersion.ToString(),
                    ProfileName = profileName,
                    Status = JobStatus.InProgress,
                    StartedUtc = DateTime.UtcNow,
                    Items = selectedItems
                };
                await jobLogger.CreateJobAsync(job, ct);
                Console.Error.WriteLine($"Job {job.JobId} started.");

                var effectiveTier = ParseTier(tier);
                Directory.CreateDirectory(output);

                if (effectiveTier == MigrationTier.FullClone)
                {
                    var cloner = host.Services.GetRequiredService<IDiskCloner>();
                    var volumePath = volume ?? "C";
                    var imageFormat = ParseFormat(format);
                    var extension = imageFormat switch
                    {
                        DiskImageFormat.Vhdx => ".vhdx",
                        DiskImageFormat.Raw => ".raw",
                        _ => ".img"
                    };
                    var imagePath = Path.Combine(output, $"clone-{volumePath}{extension}");

                    Console.Error.WriteLine($"Cloning volume {volumePath}: to {imagePath}...");
                    await cloner.CloneVolumeAsync($@"\\.\{volumePath}:", imagePath, imageFormat, progress, ct);
                    progress.Complete();
                }
                else
                {
                    var packageItems = selectedItems
                        .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.Package)
                        .ToList();
                    var regFileItems = selectedItems
                        .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.RegistryFile)
                        .ToList();

                    if (packageItems.Count > 0 && effectiveTier != MigrationTier.RegistryFile)
                    {
                        var packMigrator = host.Services.GetRequiredService<IPackageMigrator>();
                        Console.Error.WriteLine($"Capturing {packageItems.Count} package-based items...");
                        await packMigrator.CaptureAsync(packageItems, output, progress, ct);
                        progress.Complete();
                    }

                    if (regFileItems.Count > 0 && effectiveTier != MigrationTier.Package)
                    {
                        var regMigrator = host.Services.GetRequiredService<IRegistryMigrator>();
                        Console.Error.WriteLine($"Capturing {regFileItems.Count} registry+file items...");
                        await regMigrator.CaptureAsync(regFileItems, output, progress, ct);
                        progress.Complete();
                    }
                }

                job.Status = JobStatus.Completed;
                job.CompletedUtc = DateTime.UtcNow;
                await jobLogger.UpdateJobAsync(job, ct);

                if (json)
                    OutputFormatter.WriteJobDetail(job, true);
                else
                    Console.WriteLine($"Capture complete. Job ID: {job.JobId}");

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

    private static MigrationTier ParseTier(string tier) => tier.ToLowerInvariant() switch
    {
        "package" => MigrationTier.Package,
        "regfile" => MigrationTier.RegistryFile,
        "clone" => MigrationTier.FullClone,
        _ => MigrationTier.Package
    };

    private static DiskImageFormat ParseFormat(string format) => format.ToLowerInvariant() switch
    {
        "img" => DiskImageFormat.Img,
        "raw" => DiskImageFormat.Raw,
        _ => DiskImageFormat.Vhdx
    };

    private static void ApplyProfile(List<MigrationItem> items, MigrationProfile profile)
    {
        foreach (var item in items)
        {
            item.IsSelected = item.ItemType switch
            {
                MigrationItemType.Application => profile.Items.Applications.Enabled,
                MigrationItemType.UserProfile => profile.Items.UserProfiles.Enabled,
                MigrationItemType.SystemSetting => profile.Items.SystemSettings.Enabled,
                MigrationItemType.BrowserData => profile.Items.BrowserData.Enabled,
                _ => true
            };
        }
    }
}
