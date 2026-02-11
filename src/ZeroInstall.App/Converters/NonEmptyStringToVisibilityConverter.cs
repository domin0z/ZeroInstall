using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Converts a string to <see cref="Visibility"/>. Non-empty = Visible, empty/null = Collapsed.
/// </summary>
public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
