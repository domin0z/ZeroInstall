using System.Globalization;
using System.Windows;
using ZeroInstall.App.Converters;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.Converters;

public class TransportConverterTests
{
    private readonly TransportMethodToVisibilityConverter _sut = new();

    [Fact]
    public void Convert_WhenMatches_ReturnsVisible()
    {
        var result = _sut.Convert(TransportMethod.NetworkShare, typeof(Visibility), "NetworkShare", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_WhenDoesNotMatch_ReturnsCollapsed()
    {
        var result = _sut.Convert(TransportMethod.ExternalStorage, typeof(Visibility), "NetworkShare", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsCollapsed()
    {
        var result = _sut.Convert(null!, typeof(Visibility), "NetworkShare", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_SftpMatch_ReturnsVisible()
    {
        var result = _sut.Convert(TransportMethod.Sftp, typeof(Visibility), "Sftp", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_SftpNoMatch_ReturnsCollapsed()
    {
        var result = _sut.Convert(TransportMethod.ExternalStorage, typeof(Visibility), "Sftp", CultureInfo.InvariantCulture);

        result.Should().Be(Visibility.Collapsed);
    }
}
