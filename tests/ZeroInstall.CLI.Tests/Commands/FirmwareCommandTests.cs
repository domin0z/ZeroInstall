using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class FirmwareCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (FirmwareCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_ReturnsCommandNamedFirmware()
    {
        var (command, _, _) = CreateCommand();

        command.Name.Should().Be("firmware");
    }

    [Fact]
    public void Create_HasDescription()
    {
        var (command, _, _) = CreateCommand();

        command.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_HasStatusSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "status");
    }

    [Fact]
    public void Create_HasBackupBcdSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "backup-bcd");
    }

    [Fact]
    public void Create_HasRestoreBcdSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "restore-bcd");
    }

    [Fact]
    public void Create_HasListBootEntriesSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "list-boot-entries");
    }

    [Fact]
    public void BackupBcdSubcommand_HasRequiredPathArgument()
    {
        var (command, _, _) = CreateCommand();
        var backupCmd = command.Subcommands.First(c => c.Name == "backup-bcd");

        backupCmd.Arguments.Should().Contain(a => a.Name == "path");
    }

    [Fact]
    public void RestoreBcdSubcommand_HasRequiredPathArgument()
    {
        var (command, _, _) = CreateCommand();
        var restoreCmd = command.Subcommands.First(c => c.Name == "restore-bcd");

        restoreCmd.Arguments.Should().Contain(a => a.Name == "path");
    }

    [Fact]
    public void StatusSubcommand_HasNoRequiredArguments()
    {
        var (command, _, _) = CreateCommand();
        var statusCmd = command.Subcommands.First(c => c.Name == "status");

        statusCmd.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void ListBootEntriesSubcommand_HasNoRequiredArguments()
    {
        var (command, _, _) = CreateCommand();
        var listCmd = command.Subcommands.First(c => c.Name == "list-boot-entries");

        listCmd.Arguments.Should().BeEmpty();
    }
}
