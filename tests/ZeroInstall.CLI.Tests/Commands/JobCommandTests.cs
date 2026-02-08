using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class JobCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (JobCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_ReturnsCommandNamedJob()
    {
        var (command, _, _) = CreateCommand();

        command.Name.Should().Be("job");
    }

    [Fact]
    public void Create_HasListSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "list");
    }

    [Fact]
    public void Create_HasShowSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "show");
    }

    [Fact]
    public void Create_HasExportSubcommand()
    {
        var (command, _, _) = CreateCommand();

        command.Subcommands.Should().Contain(c => c.Name == "export");
    }
}
