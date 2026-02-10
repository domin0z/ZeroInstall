using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Shows a panel only when the selected transport method matches the ConverterParameter.
/// ConverterParameter should be the transport method name (e.g., "NetworkShare").
/// </summary>
public sealed class TransportMethodToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TransportMethod selected || parameter is not string targetName)
            return Visibility.Collapsed;

        return selected.ToString() == targetName ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
