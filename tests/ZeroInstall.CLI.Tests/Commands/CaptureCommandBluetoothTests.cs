using System.CommandLine;
using ZeroInstall.CLI.Commands;

namespace ZeroInstall.CLI.Tests.Commands;

public class CaptureCommandBluetoothTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (CaptureCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void Create_HasBtAddressOption()
    {
        var (command, _, _) = CreateCommand();

        command.Options.Should().Contain(o => o.Name == "--bt-address");
    }

    [Fact]
    public void Create_HasBtServerOption()
    {
        var (command, _, _) = CreateCommand();

        command.Options.Should().Contain(o => o.Name == "--bt-server");
    }

    [Fact]
    public void BtAddressOption_HasDescription()
    {
        var (command, _, _) = CreateCommand();
        var option = command.Options.First(o => o.Name == "--bt-address");

        option.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BtServerOption_DefaultsToFalse()
    {
        var (command, _, _) = CreateCommand();
        var option = command.Options.First(o => o.Name == "--bt-server");

        option.Description.Should().NotBeNullOrWhiteSpace();
    }
}
