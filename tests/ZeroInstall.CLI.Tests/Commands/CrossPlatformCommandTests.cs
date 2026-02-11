using System.CommandLine;
using ZeroInstall.CLI.Commands;
using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.CLI.Tests.Commands;

public class CrossPlatformCommandTests
{
    private static (Command command, Option<bool> verbose, Option<bool> json) CreateDiscoverCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (DiscoverCommand.Create(verbose, json), verbose, json);
    }

    private static (Command command, Option<bool> verbose, Option<bool> json) CreateCaptureCommand()
    {
        var verbose = new Option<bool>("--verbose");
        var json = new Option<bool>("--json");
        return (CaptureCommand.Create(verbose, json), verbose, json);
    }

    [Fact]
    public void DiscoverCommand_HasSourcePathOption()
    {
        var (command, _, _) = CreateDiscoverCommand();

        command.Options.Should().Contain(o => o.Name == "--source-path");
    }

    [Fact]
    public void DiscoverCommand_SourcePathOption_IsOptional()
    {
        var (command, _, _) = CreateDiscoverCommand();
        var option = command.Options.First(o => o.Name == "--source-path");

        option.Required.Should().BeFalse();
    }

    [Fact]
    public void CaptureCommand_HasSourcePathOption()
    {
        var (command, _, _) = CreateCaptureCommand();

        command.Options.Should().Contain(o => o.Name == "--source-path");
    }

    [Fact]
    public void CaptureCommand_SourcePathOption_IsOptional()
    {
        var (command, _, _) = CreateCaptureCommand();
        var option = command.Options.First(o => o.Name == "--source-path");

        option.Required.Should().BeFalse();
    }

    [Fact]
    public void DiscoverCommand_SourcePathOption_HasDescription()
    {
        var (command, _, _) = CreateDiscoverCommand();
        var option = command.Options.First(o => o.Name == "--source-path");

        option.Description.Should().Contain("cross-platform");
    }

    [Fact]
    public void CaptureCommand_SourcePathOption_HasDescription()
    {
        var (command, _, _) = CreateCaptureCommand();
        var option = command.Options.First(o => o.Name == "--source-path");

        option.Description.Should().Contain("cross-platform");
    }

    [Fact]
    public void WritePlatformInfo_MacOs_FormatsCorrectly()
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);

            OutputFormatter.WritePlatformInfo(SourcePlatform.MacOs, "14.2");

            var output = writer.ToString();
            output.Should().Contain("macOS 14.2");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void WritePlatformInfo_Linux_FormatsCorrectly()
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);

            OutputFormatter.WritePlatformInfo(SourcePlatform.Linux, "Ubuntu 22.04.3 LTS");

            var output = writer.ToString();
            output.Should().Contain("Linux Ubuntu 22.04.3 LTS");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
