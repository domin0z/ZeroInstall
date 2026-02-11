using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class CaptureCommandDashboardTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (CaptureCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_HasDashboardUrlOption()
    {
        var (command, _, _) = CreateCommand();

        command.Options.Should().Contain(o => o.Name == "--dashboard-url");
    }

    [Fact]
    public void Create_HasApiKeyOption()
    {
        var (command, _, _) = CreateCommand();

        command.Options.Should().Contain(o => o.Name == "--api-key");
    }
}
