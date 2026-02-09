using System.Globalization;
using ZeroInstall.App.Converters;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.Converters;

public class StatusConverterTests
{
    private readonly MigrationItemStatusToIconConverter _statusConverter = new();

    [Theory]
    [InlineData(MigrationItemStatus.Queued, "\u25CB")]
    [InlineData(MigrationItemStatus.InProgress, "\u27F3")]
    [InlineData(MigrationItemStatus.Completed, "\u2713")]
    [InlineData(MigrationItemStatus.Failed, "\u2717")]
    [InlineData(MigrationItemStatus.Skipped, "\u2013")]
    [InlineData(MigrationItemStatus.Warning, "\u26A0")]
    public void StatusToIcon_MapsCorrectly(MigrationItemStatus status, string expected)
    {
        var result = _statusConverter.Convert(status, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void StatusToIcon_NonStatus_ReturnsQuestionMark()
    {
        var result = _statusConverter.Convert("not a status", typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("?");
    }

    private readonly PercentToWidthConverter _percentConverter = new();

    [Fact]
    public void PercentToWidth_HalfPercent_ReturnsHalfWidth()
    {
        var result = _percentConverter.Convert(0.5, typeof(double), "400", CultureInfo.InvariantCulture);
        result.Should().Be(200.0);
    }

    [Fact]
    public void PercentToWidth_ZeroPercent_ReturnsZero()
    {
        var result = _percentConverter.Convert(0.0, typeof(double), "400", CultureInfo.InvariantCulture);
        result.Should().Be(0.0);
    }

    [Fact]
    public void PercentToWidth_FullPercent_ReturnsMaxWidth()
    {
        var result = _percentConverter.Convert(1.0, typeof(double), "400", CultureInfo.InvariantCulture);
        result.Should().Be(400.0);
    }

    [Fact]
    public void PercentToWidth_OverOnePercent_ClampsToMaxWidth()
    {
        var result = _percentConverter.Convert(1.5, typeof(double), "400", CultureInfo.InvariantCulture);
        result.Should().Be(400.0);
    }

    [Fact]
    public void PercentToWidth_InvalidParameter_ReturnsZero()
    {
        var result = _percentConverter.Convert(0.5, typeof(double), "notanumber", CultureInfo.InvariantCulture);
        result.Should().Be(0.0);
    }
}
