using System.Globalization;
using System.Windows.Data;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Converts a byte count (long) to a human-readable size string (e.g., "1.2 GB").
/// </summary>
public sealed class BytesToHumanReadableConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes <= 0)
            return "0 B";

        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < Units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return order == 0
            ? $"{size:F0} {Units[order]}"
            : $"{size:F1} {Units[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
