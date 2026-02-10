using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim restore --input path [--user-map src:dest ...] [--create-users]
///             [--sftp-host host] [--sftp-port port] [--sftp-user user] [--sftp-pass pass]
///             [--sftp-key path] [--sftp-path path] [--encrypt passphrase] [--no-compress]
///             [--json] [--verbose]
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

        var sftpHostOption = new Option<string?>("--sftp-host")
        {
            Description = "SFTP server hostname to download capture from"
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
            Description = "Encryption passphrase for AES-256 decryption"
        };

        var noCompressOption = new Option<bool>("--no-compress")
        {
            Description = "Data was not compressed during upload"
        };

        var command = new Command("restore", "Restore captured data to this machine")
        {
            inputOption, userMapOption, createUsersOption,
            sftpHostOption, sftpPortOption, sftpUserOption, sftpPassOption, sftpKeyOption,
            sftpPathOption, encryptOption, noCompressOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var input = parseResult.GetValue(inputOption)!;
            var userMaps = parseResult.GetValue(userMapOption) ?? Array.Empty<string>();
            var createUsers = parseResult.GetValue(createUsersOption);
            var sftpHost = parseResult.GetValue(sftpHostOption);
            var sftpPort = parseResult.GetValue(sftpPortOption);
            var sftpUser = parseResult.GetValue(sftpUserOption);
            var sftpPass = parseResult.GetValue(sftpPassOption);
            var sftpKey = parseResult.GetValue(sftpKeyOption);
            var sftpPath = parseResult.GetValue(sftpPathOption) ?? "/backups/zim";
            var encryptPassphrase = parseResult.GetValue(encryptOption);
            var noCompress = parseResult.GetValue(noCompressOption);

            using var host = CliHost.BuildHost(verbose);
            var jobLogger = host.Services.GetRequiredService<IJobLogger>();
            var progress = new ConsoleProgressReporter();

            try
            {
                // SFTP download if configured
                if (!string.IsNullOrEmpty(sftpHost))
                {
                    Console.Error.WriteLine($"Downloading capture from SFTP server {sftpHost}...");
                    Directory.CreateDirectory(input);

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

                    // Download manifest to determine what files to get
                    var manifest = await sftpTransport.ReceiveManifestAsync(ct);
                    Console.Error.WriteLine($"Found capture from {manifest.SourceHostname} with {manifest.Items.Count} items.");

                    // Download all data files
                    var remoteFiles = await sftpTransport.ListRemoteDirectoryAsync(
                        sftpPath + "/zim-data", ct);
                    foreach (var file in remoteFiles)
                    {
                        if (file.IsDirectory) continue;

                        var metadata = new TransferMetadata
                        {
                            RelativePath = file.Name,
                            SizeBytes = file.Length,
                            IsCompressed = !noCompress,
                            IsEncrypted = !string.IsNullOrEmpty(encryptPassphrase)
                        };
                        await using var stream = await sftpTransport.ReceiveAsync(metadata, ct);
                        var localPath = Path.Combine(input, file.Name);
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        await using var localFile = File.Create(localPath);
                        await stream.CopyToAsync(localFile, ct);
                    }

                    progress.Complete();
                    Console.Error.WriteLine("SFTP download complete.");
                }

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
