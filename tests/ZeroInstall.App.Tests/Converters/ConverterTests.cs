using System.Globalization;
using System.Windows;
using ZeroInstall.App.Converters;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Tests.Converters;

public class ConverterTests
{
    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void BoolToVisibility_ConvertsCorrectly(bool input, Visibility expected)
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1_048_576L, "1.0 MB")]
    [InlineData(1_073_741_824L, "1.0 GB")]
    [InlineData(1_099_511_627_776L, "1.0 TB")]
    [InlineData(536_870_912L, "512.0 MB")]
    public void BytesToHumanReadable_ConvertsCorrectly(long bytes, string expected)
    {
        var converter = new BytesToHumanReadableConverter();
        var result = converter.Convert(bytes, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InverseBool_InvertsCorrectly(bool input, bool expected)
    {
        var converter = new InverseBoolConverter();
        var result = converter.Convert(input, typeof(bool), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MigrationTier.Package, "Package")]
    [InlineData(MigrationTier.RegistryFile, "Reg+File")]
    [InlineData(MigrationTier.FullClone, "Clone")]
    public void MigrationTierToBadge_ConvertsCorrectly(MigrationTier tier, string expected)
    {
        var converter = new MigrationTierToBadgeConverter();
        var result = converter.Convert(tier, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }
}
