using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class BitLockerCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (BitLockerCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_ReturnsCommandNamedBitLocker()
    {
        var (command, _, _) = CreateCommand();

        command.Name.Should().Be("bitlocker");
    }

    [Fact]
    public void Create_HasStatusSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "status");
    }

    [Fact]
    public void Create_HasUnlockSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "unlock");
    }

    [Fact]
    public void Create_HasDescription()
    {
        var (command, _, _) = CreateCommand();

        command.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StatusSubcommand_HasOptionalVolumeArgument()
    {
        var (command, _, _) = CreateCommand();
        var statusCmd = command.Subcommands.First(c => c.Name == "status");

        statusCmd.Arguments.Should().Contain(a => a.Name == "volume");
    }

    [Fact]
    public void UnlockSubcommand_HasRequiredVolumeArgument()
    {
        var (command, _, _) = CreateCommand();
        var unlockCmd = command.Subcommands.First(c => c.Name == "unlock");

        unlockCmd.Arguments.Should().Contain(a => a.Name == "volume");
    }

    [Fact]
    public void UnlockSubcommand_HasRecoveryPasswordOption()
    {
        var (command, _, _) = CreateCommand();
        var unlockCmd = command.Subcommands.First(c => c.Name == "unlock");

        unlockCmd.Options.Should().Contain(o => o.Name == "--recovery-password");
    }

    [Fact]
    public void UnlockSubcommand_HasRecoveryKeyOption()
    {
        var (command, _, _) = CreateCommand();
        var unlockCmd = command.Subcommands.First(c => c.Name == "unlock");

        unlockCmd.Options.Should().Contain(o => o.Name == "--recovery-key");
    }
}
