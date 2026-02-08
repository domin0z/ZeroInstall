using ZeroInstall.CLI.Infrastructure;

namespace ZeroInstall.CLI.Tests.Infrastructure;

public class ConsoleProgressReporterTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(1024, "1.0 KB/s")]
    [InlineData(1_048_576, "1.0 MB/s")]
    [InlineData(1_073_741_824, "1.0 GB/s")]
    [InlineData(500, "500 B/s")]
    public void FormatSpeed_ReturnsCorrectString(long bytesPerSecond, string expected)
    {
        ConsoleProgressReporter.FormatSpeed(bytesPerSecond).Should().Be(expected);
    }

    [Fact]
    public void FormatEta_NullReturnsEmpty()
    {
        ConsoleProgressReporter.FormatEta(null).Should().BeEmpty();
    }

    [Fact]
    public void FormatEta_MinutesRange()
    {
        var eta = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(30));

        var result = ConsoleProgressReporter.FormatEta(eta);

        result.Should().Contain("~3:30 remaining");
    }
}
