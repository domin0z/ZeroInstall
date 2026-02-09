using System.Globalization;
using System.Windows.Data;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.App.Converters;

/// <summary>
/// Converts a <see cref="MigrationItemStatus"/> to a single-character icon string.
/// </summary>
public sealed class MigrationItemStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MigrationItemStatus status
            ? status switch
            {
                MigrationItemStatus.Queued => "\u25CB",      // ○
                MigrationItemStatus.InProgress => "\u27F3",   // ⟳
                MigrationItemStatus.Completed => "\u2713",    // ✓
                MigrationItemStatus.Failed => "\u2717",       // ✗
                MigrationItemStatus.Skipped => "\u2013",      // –
                MigrationItemStatus.Warning => "\u26A0",      // ⚠
                _ => "?"
            }
            : "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
