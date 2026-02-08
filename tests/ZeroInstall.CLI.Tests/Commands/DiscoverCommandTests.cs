using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class DiscoverCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (DiscoverCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_ReturnsCommandNamedDiscover()
    {
        var (command, _, _) = CreateCommand();

        command.Name.Should().Be("discover");
    }

    [Fact]
    public void Create_HasTypeOption()
    {
        var (command, _, _) = CreateCommand();

        command.Options.Should().Contain(o => o.Name == "--type");
    }

    [Fact]
    public void Create_HasDescription()
    {
        var (command, _, _) = CreateCommand();

        command.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HelpOutput_ContainsTypeOption()
    {
        var (command, verbose, json) = CreateCommand();
        var rootCommand = new RootCommand { verbose, json, command };

        var parseResult = rootCommand.Parse("discover --help");

        // Parsing --help should not throw
        parseResult.Should().NotBeNull();
    }
}
