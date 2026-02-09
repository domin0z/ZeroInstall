using System.Globalization;
using System.Windows.Data;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Converts a <see cref="MigrationTier"/> to a short badge label string.
/// </summary>
public sealed class MigrationTierToBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MigrationTier tier
            ? tier switch
            {
                MigrationTier.Package => "Package",
                MigrationTier.RegistryFile => "Reg+File",
                MigrationTier.FullClone => "Clone",
                _ => tier.ToString()
            }
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
