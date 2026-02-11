using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Commands;

/// <summary>
/// zim domain status
/// zim domain join --domain &lt;domain&gt; [--ou &lt;ou&gt;] [--computer-name &lt;name&gt;] [--username &lt;user&gt;] [--password &lt;pass&gt;]
/// zim domain unjoin [--workgroup &lt;name&gt;] [--username &lt;user&gt;] [--password &lt;pass&gt;]
/// zim domain rename --name &lt;name&gt; [--username &lt;user&gt;] [--password &lt;pass&gt;]
/// zim domain migrate-profile --old-sid &lt;sid&gt; --new-sid &lt;sid&gt; --profile-path &lt;path&gt; [--rename-folder &lt;name&gt;]
/// </summary>
internal static class DomainCommand
{
    public static Command Create(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("domain", "Domain migration: detect status, join/unjoin, rename, reassign profiles");

        command.Add(CreateStatusCommand(verboseOption, jsonOption));
        command.Add(CreateJoinCommand(verboseOption, jsonOption));
        command.Add(CreateUnjoinCommand(verboseOption, jsonOption));
        command.Add(CreateRenameCommand(verboseOption, jsonOption));
        command.Add(CreateMigrateProfileCommand(verboseOption, jsonOption));

        return command;
    }

    private static Command CreateStatusCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var command = new Command("status", "Show domain/workgroup/Azure AD join status");

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);

            using var host = CliHost.BuildHost(verbose);
            var domainService = host.Services.GetRequiredService<IDomainService>();

            try
            {
                var info = await domainService.GetDomainInfoAsync(ct);
                OutputFormatter.WriteDomainInfo(info, json);
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

    private static Command CreateJoinCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var domainOption = new Option<string>("--domain")
        {
            Description = "Domain to join (e.g., corp.local)",
            Required = true
        };

        var ouOption = new Option<string?>("--ou")
        {
            Description = "OU path for the computer account (e.g., OU=PCs,DC=corp,DC=local)"
        };

        var computerNameOption = new Option<string?>("--computer-name")
        {
            Description = "New computer name to set during domain join"
        };

        var usernameOption = new Option<string>("--username")
        {
            Description = "Domain admin username",
            Required = true
        };

        var passwordOption = new Option<string>("--password")
        {
            Description = "Domain admin password",
            Required = true
        };

        var command = new Command("join", "Join this machine to an Active Directory domain")
        {
            domainOption,
            ouOption,
            computerNameOption,
            usernameOption,
            passwordOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var domain = parseResult.GetValue(domainOption)!;
            var ou = parseResult.GetValue(ouOption);
            var computerName = parseResult.GetValue(computerNameOption);
            var username = parseResult.GetValue(usernameOption)!;
            var password = parseResult.GetValue(passwordOption)!;

            using var host = CliHost.BuildHost(verbose);
            var joinService = host.Services.GetRequiredService<IDomainJoinService>();

            var creds = new DomainCredentials
            {
                Domain = domain,
                Username = username,
                Password = password
            };

            try
            {
                var (success, message) = await joinService.JoinDomainAsync(
                    domain, ou, creds, computerName, ct);
                OutputFormatter.WriteDomainJoinResult(success, message, json);
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

    private static Command CreateUnjoinCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var workgroupOption = new Option<string>("--workgroup")
        {
            Description = "Workgroup name (default: WORKGROUP)",
            DefaultValueFactory = _ => "WORKGROUP"
        };

        var usernameOption = new Option<string?>("--username")
        {
            Description = "Domain admin username (optional)"
        };

        var passwordOption = new Option<string?>("--password")
        {
            Description = "Domain admin password (optional)"
        };

        var command = new Command("unjoin", "Unjoin this machine from a domain back to a workgroup")
        {
            workgroupOption,
            usernameOption,
            passwordOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var workgroup = parseResult.GetValue(workgroupOption)!;
            var username = parseResult.GetValue(usernameOption);
            var password = parseResult.GetValue(passwordOption);

            using var host = CliHost.BuildHost(verbose);
            var joinService = host.Services.GetRequiredService<IDomainJoinService>();

            DomainCredentials? creds = null;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                creds = new DomainCredentials
                {
                    Domain = "",
                    Username = username,
                    Password = password
                };
            }

            try
            {
                var (success, message) = await joinService.UnjoinDomainAsync(workgroup, creds, ct);
                OutputFormatter.WriteDomainJoinResult(success, message, json);
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

    private static Command CreateRenameCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var nameOption = new Option<string>("--name")
        {
            Description = "New computer name",
            Required = true
        };

        var usernameOption = new Option<string?>("--username")
        {
            Description = "Domain admin username (required if domain-joined)"
        };

        var passwordOption = new Option<string?>("--password")
        {
            Description = "Domain admin password (required if domain-joined)"
        };

        var command = new Command("rename", "Rename this computer")
        {
            nameOption,
            usernameOption,
            passwordOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(nameOption)!;
            var username = parseResult.GetValue(usernameOption);
            var password = parseResult.GetValue(passwordOption);

            using var host = CliHost.BuildHost(verbose);
            var joinService = host.Services.GetRequiredService<IDomainJoinService>();

            DomainCredentials? creds = null;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                creds = new DomainCredentials
                {
                    Domain = "",
                    Username = username,
                    Password = password
                };
            }

            try
            {
                var (success, message) = await joinService.RenameComputerAsync(name, creds, ct);
                OutputFormatter.WriteDomainJoinResult(success, message, json);
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

    private static Command CreateMigrateProfileCommand(Option<bool> verboseOption, Option<bool> jsonOption)
    {
        var oldSidOption = new Option<string>("--old-sid")
        {
            Description = "Source SID of the profile to reassign",
            Required = true
        };

        var newSidOption = new Option<string>("--new-sid")
        {
            Description = "Destination SID to reassign the profile to",
            Required = true
        };

        var profilePathOption = new Option<string>("--profile-path")
        {
            Description = "Path to the user profile directory (e.g., C:\\Users\\Bill)",
            Required = true
        };

        var renameFolderOption = new Option<string?>("--rename-folder")
        {
            Description = "Optionally rename the profile folder after reassignment"
        };

        var command = new Command("migrate-profile", "Reassign a user profile from one SID to another in-place")
        {
            oldSidOption,
            newSidOption,
            profilePathOption,
            renameFolderOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(verboseOption);
            var json = parseResult.GetValue(jsonOption);
            var oldSid = parseResult.GetValue(oldSidOption)!;
            var newSid = parseResult.GetValue(newSidOption)!;
            var profilePath = parseResult.GetValue(profilePathOption)!;
            var renameFolder = parseResult.GetValue(renameFolderOption);

            using var host = CliHost.BuildHost(verbose);
            var reassignService = host.Services.GetRequiredService<IProfileReassignmentService>();

            try
            {
                var (success, message) = await reassignService.ReassignProfileAsync(
                    oldSid, newSid, profilePath, ct);
                OutputFormatter.WriteDomainJoinResult(success, message, json);

                if (success && !string.IsNullOrWhiteSpace(renameFolder))
                {
                    var (renameSuccess, renameMessage) = await reassignService.RenameProfileFolderAsync(
                        profilePath, renameFolder, ct);
                    OutputFormatter.WriteDomainJoinResult(renameSuccess, renameMessage, json);
                    return renameSuccess ? 0 : 1;
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
