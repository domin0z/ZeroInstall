using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Models;

public class TransferProgressTests
{
    [Fact]
    public void OverallPercentage_WhenZeroTotal_ReturnsZero()
    {
        var progress = new TransferProgress
        {
            OverallBytesTransferred = 0,
            OverallTotalBytes = 0
        };

        progress.OverallPercentage.Should().Be(0);
    }

    [Fact]
    public void OverallPercentage_WhenHalfway_ReturnsFiftyPercent()
    {
        var progress = new TransferProgress
        {
            OverallBytesTransferred = 500,
            OverallTotalBytes = 1000
        };

        progress.OverallPercentage.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void OverallPercentage_WhenComplete_ReturnsOne()
    {
        var progress = new TransferProgress
        {
            OverallBytesTransferred = 1000,
            OverallTotalBytes = 1000
        };

        progress.OverallPercentage.Should().BeApproximately(1.0, 0.001);
    }
}
