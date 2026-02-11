using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

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

        var sftpHostOption = new Option<string?>("--sftp-host")
        {
            Description = "SFTP server hostname for remote upload"
        };

        var sftpPortOption = new Option<int>("--sftp-port")
        {
            Description = "SFTP server port",
            DefaultValueFactory = _ => 22
        };

        var sftpUserOption = new Option<string?>("--sftp-user")
        {
            Description = "SFTP username"
        };

        var sftpPassOption = new Option<string?>("--sftp-pass")
        {
            Description = "SFTP password"
        };

        var sftpKeyOption = new Option<string?>("--sftp-key")
        {
            Description = "Path to SSH private key file"
        };

        var sftpPathOption = new Option<string>("--sftp-path")
        {
            Description = "Remote base path on SFTP server",
            DefaultValueFactory = _ => "/backups/zim"
        };

        var encryptOption = new Option<string?>("--encrypt")
        {
            Description = "Encryption passphrase for AES-256 encryption"
        };

        var noCompressOption = new Option<bool>("--no-compress")
        {
            Description = "Disable compression before upload"
        };

        var btAddressOption = new Option<string?>("--bt-address")
        {
            Description = "Bluetooth address of the destination machine (client mode)"
        };

        var btServerOption = new Option<bool>("--bt-server")
        {
            Description = "Run as Bluetooth server (listen for incoming connection)"
        };

        var sourcePathOption = new Option<string?>("--source-path")
        {
            Description = "Path to mounted foreign drive (macOS/Linux) for cross-platform capture"
        };

        var dashboardUrlOption = new Option<string?>("--dashboard-url")
        {
            Description = "Dashboard base URL to push job data to (e.g., http://server:5180)"
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description = "API key for dashboard authentication"
        };

        var command = new Command("capture", "Capture data from this machine for migration")
        {
            outputOption, tierOption, profileOption, allOption, volumeOption, formatOption,
            sftpHostOption, sftpPortOption, sftpUserOption, sftpPassOption, sftpKeyOption,
            sftpPathOption, encryptOption, noCompressOption,
            btAddressOption, btServerOption, sourcePathOption,
            dashboardUrlOption, apiKeyOption
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
            var sftpHost = parseResult.GetValue(sftpHostOption);
            var sftpPort = parseResult.GetValue(sftpPortOption);
            var sftpUser = parseResult.GetValue(sftpUserOption);
            var sftpPass = parseResult.GetValue(sftpPassOption);
            var sftpKey = parseResult.GetValue(sftpKeyOption);
            var sftpPath = parseResult.GetValue(sftpPathOption) ?? "/backups/zim";
            var encryptPassphrase = parseResult.GetValue(encryptOption);
            var noCompress = parseResult.GetValue(noCompressOption);

            var sourcePath = parseResult.GetValue(sourcePathOption);
            var dashboardUrl = parseResult.GetValue(dashboardUrlOption);
            var apiKey = parseResult.GetValue(apiKeyOption);

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();
            var profileManager = host.Services.GetRequiredService<IProfileManager>();
            var progress = new ConsoleProgressReporter();

            try
            {
                // Discover items
                Console.Error.WriteLine("Discovering items...");

                IReadOnlyList<MigrationItem> discoveredItems;
                SourcePlatform detectedPlatform = SourcePlatform.Windows;

                if (!string.IsNullOrEmpty(sourcePath))
                {
                    var crossPlatform = host.Services.GetRequiredService<ICrossPlatformDiscoveryService>();
                    var result = await crossPlatform.DiscoverAllAsync(sourcePath, ct);
                    detectedPlatform = result.Platform;

                    OutputFormatter.WritePlatformInfo(result.Platform, result.OsVersion);

                    var migrationItems = new List<MigrationItem>();
                    foreach (var app in result.Applications)
                    {
                        migrationItems.Add(new MigrationItem
                        {
                            DisplayName = app.Name,
                            ItemType = MigrationItemType.Application,
                            RecommendedTier = MigrationTier.Package,
                            EstimatedSizeBytes = app.EstimatedSizeBytes,
                            SourceData = app
                        });
                    }
                    foreach (var profile in result.UserProfiles)
                    {
                        migrationItems.Add(new MigrationItem
                        {
                            DisplayName = profile.Username,
                            ItemType = MigrationItemType.UserProfile,
                            RecommendedTier = MigrationTier.Package,
                            EstimatedSizeBytes = profile.EstimatedSizeBytes,
                            SourceData = profile
                        });
                    }
                    discoveredItems = migrationItems.AsReadOnly();
                }
                else
                {
                    var discovery = host.Services.GetRequiredService<IDiscoveryService>();
                    discoveredItems = await discovery.DiscoverAllAsync(progress, ct);
                }

                progress.Complete();
                var items = discoveredItems;
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

                // Cross-platform sources only support Tier 1 (package) migration
                if (detectedPlatform != SourcePlatform.Windows && detectedPlatform != SourcePlatform.Unknown)
                {
                    if (effectiveTier == MigrationTier.FullClone)
                    {
                        Console.Error.WriteLine("WARNING: Full disk clone is not supported for cross-platform sources. " +
                                                "Only package-based migration and profile/file transfer are available.");
                        job.Status = JobStatus.Failed;
                        job.CompletedUtc = DateTime.UtcNow;
                        await jobLogger.UpdateJobAsync(job, ct);
                        return 1;
                    }

                    if (effectiveTier == MigrationTier.RegistryFile)
                    {
                        Console.Error.WriteLine("WARNING: Registry capture is not supported for cross-platform sources. " +
                                                "Only package-based migration and profile/file transfer are available.");
                        job.Status = JobStatus.Failed;
                        job.CompletedUtc = DateTime.UtcNow;
                        await jobLogger.UpdateJobAsync(job, ct);
                        return 1;
                    }
                }

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

                // SFTP upload if configured
                if (!string.IsNullOrEmpty(sftpHost))
                {
                    Console.Error.WriteLine($"Uploading capture to SFTP server {sftpHost}...");
                    using var sftpClient = new SftpClientWrapper(
                        sftpHost, sftpPort, sftpUser ?? "anonymous", sftpPass, sftpKey);
                    using var sftpTransport = new SftpTransport(
                        sftpClient, sftpPath,
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<SftpTransport>.Instance,
                        encryptPassphrase, !noCompress);

                    var connected = await sftpTransport.TestConnectionAsync(ct);
                    if (!connected)
                    {
                        Console.Error.WriteLine("Error: Could not connect to SFTP server.");
                        return 1;
                    }

                    // Upload all files from output directory
                    var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(output, file).Replace('\\', '/');
                        var fileInfo = new FileInfo(file);
                        await using var fileStream = File.OpenRead(file);
                        var metadata = new TransferMetadata
                        {
                            RelativePath = relativePath,
                            SizeBytes = fileInfo.Length,
                            Checksum = await ChecksumHelper.ComputeAsync(fileStream, ct)
                        };
                        fileStream.Position = 0;
                        await sftpTransport.SendAsync(fileStream, metadata, progress, ct);
                    }

                    progress.Complete();
                    Console.Error.WriteLine("SFTP upload complete.");
                }

                job.Status = JobStatus.Completed;
                job.CompletedUtc = DateTime.UtcNow;
                await jobLogger.UpdateJobAsync(job, ct);

                // Push to dashboard if configured
                if (!string.IsNullOrEmpty(dashboardUrl) && !string.IsNullOrEmpty(apiKey))
                {
                    using var dashboard = new DashboardClient(dashboardUrl, apiKey);
                    await dashboard.PushJobAsync(job, ct);
                    var report = await jobLogger.GenerateReportAsync(job.JobId, ct);
                    await dashboard.PushReportAsync(report, ct);
                }

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
