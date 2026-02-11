using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class DomainCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (DomainCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_ReturnsCommandNamedDomain()
    {
        var (command, _, _) = CreateCommand();

        command.Name.Should().Be("domain");
    }

    [Fact]
    public void Create_HasDescription()
    {
        var (command, _, _) = CreateCommand();

        command.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_HasFiveSubcommands()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().HaveCount(5);
        command.Subcommands.Select(c => c.Name).Should().BeEquivalentTo(
            "status", "join", "unjoin", "rename", "migrate-profile");
    }

    [Fact]
    public void StatusSubcommand_HasNoRequiredArgs()
    {
        var (command, _, _) = CreateCommand();
        var statusCmd = command.Subcommands.First(c => c.Name == "status");

        statusCmd.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void JoinSubcommand_HasRequiredDomainOption()
    {
        var (command, _, _) = CreateCommand();
        var joinCmd = command.Subcommands.First(c => c.Name == "join");

        joinCmd.Options.Should().Contain(o => o.Name == "--domain");
        joinCmd.Options.Should().Contain(o => o.Name == "--username");
        joinCmd.Options.Should().Contain(o => o.Name == "--password");
    }

    [Fact]
    public void JoinSubcommand_HasOptionalOuAndComputerName()
    {
        var (command, _, _) = CreateCommand();
        var joinCmd = command.Subcommands.First(c => c.Name == "join");

        joinCmd.Options.Should().Contain(o => o.Name == "--ou");
        joinCmd.Options.Should().Contain(o => o.Name == "--computer-name");
    }

    [Fact]
    public void UnjoinSubcommand_HasOptionalWorkgroupOption()
    {
        var (command, _, _) = CreateCommand();
        var unjoinCmd = command.Subcommands.First(c => c.Name == "unjoin");

        unjoinCmd.Options.Should().Contain(o => o.Name == "--workgroup");
    }

    [Fact]
    public void RenameSubcommand_HasRequiredNameOption()
    {
        var (command, _, _) = CreateCommand();
        var renameCmd = command.Subcommands.First(c => c.Name == "rename");

        renameCmd.Options.Should().Contain(o => o.Name == "--name");
    }

    [Fact]
    public void MigrateProfileSubcommand_HasRequiredArgs()
    {
        var (command, _, _) = CreateCommand();
        var migrateCmd = command.Subcommands.First(c => c.Name == "migrate-profile");

        migrateCmd.Options.Should().Contain(o => o.Name == "--old-sid");
        migrateCmd.Options.Should().Contain(o => o.Name == "--new-sid");
        migrateCmd.Options.Should().Contain(o => o.Name == "--profile-path");
    }

    [Fact]
    public void MigrateProfileSubcommand_HasOptionalRenameFolder()
    {
        var (command, _, _) = CreateCommand();
        var migrateCmd = command.Subcommands.First(c => c.Name == "migrate-profile");

        migrateCmd.Options.Should().Contain(o => o.Name == "--rename-folder");
    }

    [Fact]
    public void UnjoinSubcommand_HasOptionalCredentials()
    {
        var (command, _, _) = CreateCommand();
        var unjoinCmd = command.Subcommands.First(c => c.Name == "unjoin");

        unjoinCmd.Options.Should().Contain(o => o.Name == "--username");
        unjoinCmd.Options.Should().Contain(o => o.Name == "--password");
    }

    [Fact]
    public void RenameSubcommand_HasOptionalCredentials()
    {
        var (command, _, _) = CreateCommand();
        var renameCmd = command.Subcommands.First(c => c.Name == "rename");

        renameCmd.Options.Should().Contain(o => o.Name == "--username");
        renameCmd.Options.Should().Contain(o => o.Name == "--password");
    }
}
