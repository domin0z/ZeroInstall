using System.Globalization;
using System.Windows;
using ZeroInstall.App.Converters;

namespace ZeroInstall.App.Tests.Converters;

public class NonEmptyStringToVisibilityConverterTests
{
    private readonly NonEmptyStringToVisibilityConverter _sut = new();

    [Fact]
    public void Convert_NonEmptyString_ReturnsVisible()
    {
        var result = _sut.Convert("hello", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_EmptyString_ReturnsCollapsed()
    {
        var result = _sut.Convert("", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_NullValue_ReturnsCollapsed()
    {
        var result = _sut.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }
}
