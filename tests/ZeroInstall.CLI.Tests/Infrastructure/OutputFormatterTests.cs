using ZeroInstall.CLI.Infrastructure;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.CLI.Tests.Infrastructure;

public class OutputFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1_048_576, "1.0 MB")]
    [InlineData(1_073_741_824, "1.0 GB")]
    [InlineData(1_099_511_627_776, "1.0 TB")]
    [InlineData(1_536, "1.5 KB")]
    [InlineData(-1, "0 B")]
    public void FormatBytes_ReturnsCorrectString(long bytes, string expected)
    {
        OutputFormatter.FormatBytes(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Hi", "Hi")]
    [InlineData("Hello World", "Hello W...")]
    public void Truncate_RespectsMaxLength(string? value, string expected)
    {
        OutputFormatter.Truncate(value, 10).Should().Be(expected);
    }

    [Fact]
    public void WriteDiscoveryResults_Table_WritesToConsole()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Chrome",
                ItemType = MigrationItemType.Application,
                RecommendedTier = MigrationTier.Package,
                EstimatedSizeBytes = 1_048_576
            }
        };

        var output = CaptureConsoleOutput(() =>
            OutputFormatter.WriteDiscoveryResults(items, false));

        output.Should().Contain("Chrome");
        output.Should().Contain("Application");
        output.Should().Contain("Package");
        output.Should().Contain("1.0 MB");
    }

    [Fact]
    public void WriteDiscoveryResults_Json_OutputsValidJson()
    {
        var items = new List<MigrationItem>
        {
            new()
            {
                DisplayName = "Chrome",
                ItemType = MigrationItemType.Application,
                RecommendedTier = MigrationTier.Package
            }
        };

        var output = CaptureConsoleOutput(() =>
            OutputFormatter.WriteDiscoveryResults(items, true));

        output.Should().Contain("\"displayName\"");
        output.Should().Contain("Chrome");
    }

    [Fact]
    public void WriteJobList_EmptyList_ShowsMessage()
    {
        var jobs = new List<MigrationJob>();

        var output = CaptureConsoleOutput(() =>
            OutputFormatter.WriteJobList(jobs, false));

        output.Should().Contain("No jobs found.");
    }

    [Fact]
    public void WriteProfileList_EmptyList_ShowsMessage()
    {
        var profiles = new List<MigrationProfile>();

        var output = CaptureConsoleOutput(() =>
            OutputFormatter.WriteProfileList(profiles, false));

        output.Should().Contain("No profiles found.");
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
