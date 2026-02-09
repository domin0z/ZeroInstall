using System.Globalization;
using System.Windows.Data;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Multiplies a 0.0–1.0 percentage by a parameter (max width) to produce a pixel width.
/// </summary>
public sealed class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent
            && parameter is string paramStr
            && double.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxWidth))
        {
            return Math.Max(0, Math.Min(maxWidth, percent * maxWidth));
        }

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
