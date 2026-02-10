using ZeroInstall.Backup.Tray;

namespace ZeroInstall.Backup.Tests.Tray;

public class BackupStatusFormTests
{
    [Theory]
    [InlineData(1_073_741_824L, "1.0 GB")]
    [InlineData(5_368_709_120L, "5.0 GB")]
    [InlineData(1_048_576L, "1.0 MB")]
    [InlineData(512_000L, "500.0 KB")]
    [InlineData(100L, "100 B")]
    public void FormatBytes_ReturnsHumanReadable(long bytes, string expected)
    {
        BackupStatusForm.FormatBytes(bytes).Should().Be(expected);
    }
}
