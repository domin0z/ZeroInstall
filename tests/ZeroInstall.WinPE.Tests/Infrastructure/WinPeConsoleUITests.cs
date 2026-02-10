using ZeroInstall.WinPE.Infrastructure;

namespace ZeroInstall.WinPE.Tests.Infrastructure;

public class WinPeConsoleUITests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1.00 KB")]
    [InlineData(1048576L, "1.00 MB")]
    [InlineData(1073741824L, "1.00 GB")]
    [InlineData(500107862016L, "465.76 GB")]
    public void FormatBytes_FormatsCorrectly(long input, string expected)
    {
        WinPeConsoleUI.FormatBytes(input).Should().Be(expected);
    }

    [Fact]
    public void FormatTimeSpan_Seconds_FormatsCorrectly()
    {
        WinPeConsoleUI.FormatTimeSpan(TimeSpan.FromSeconds(45)).Should().Be("45s");
    }

    [Fact]
    public void FormatTimeSpan_Minutes_FormatsCorrectly()
    {
        WinPeConsoleUI.FormatTimeSpan(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30))
            .Should().Be("5m 30s");
    }

    [Fact]
    public void FormatTimeSpan_Hours_FormatsCorrectly()
    {
        WinPeConsoleUI.FormatTimeSpan(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15))
            .Should().Be("2h 15m");
    }

    [Fact]
    public void WriteHeader_DoesNotThrow()
    {
        var action = () => WinPeConsoleUI.WriteHeader();
        action.Should().NotThrow();
    }
}
