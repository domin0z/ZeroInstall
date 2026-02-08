using ZeroInstall.Core.Models;

namespace ZeroInstall.CLI.Infrastructure;

/// <summary>
/// Reports transfer progress to the console using single-line overwrite on stderr.
/// Keeps stdout clean for piping/JSON output.
/// </summary>
internal sealed class ConsoleProgressReporter : IProgress<TransferProgress>, IProgress<string>
{
    private readonly object _lock = new();

    public void Report(TransferProgress value)
    {
        lock (_lock)
        {
            var percent = (int)(value.OverallPercentage * 100);
            var speed = FormatSpeed(value.BytesPerSecond);
            var eta = FormatEta(value.EstimatedTimeRemaining);
            var itemInfo = $"[{value.CurrentItemIndex}/{value.TotalItems}] {Truncate(value.CurrentItemName, 30)}";

            var line = $"\r{itemInfo}... {percent}% ({speed}{eta})";
            // Pad to overwrite previous longer lines
            var padded = line.PadRight(Console.WindowWidth > 0 ? Math.Min(Console.WindowWidth - 1, 120) : 80);

            Console.Error.Write(padded);
        }
    }

    public void Report(string value)
    {
        lock (_lock)
        {
            var padded = $"\r{value}".PadRight(Console.WindowWidth > 0 ? Math.Min(Console.WindowWidth - 1, 120) : 80);
            Console.Error.Write(padded);
        }
    }

    /// <summary>
    /// Clears the progress line.
    /// </summary>
    public void Complete()
    {
        Console.Error.Write('\r' + new string(' ', Console.WindowWidth > 0 ? Math.Min(Console.WindowWidth - 1, 120) : 80) + '\r');
    }

    internal static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "";
        if (bytesPerSecond >= 1_073_741_824) return $"{bytesPerSecond / 1_073_741_824.0:F1} GB/s";
        if (bytesPerSecond >= 1_048_576) return $"{bytesPerSecond / 1_048_576.0:F1} MB/s";
        if (bytesPerSecond >= 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond} B/s";
    }

    internal static string FormatEta(TimeSpan? eta)
    {
        if (eta is null || eta.Value.TotalSeconds <= 0)
            return "";

        var ts = eta.Value;
        if (ts.TotalHours >= 1)
            return $", ~{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2} remaining";
        if (ts.TotalMinutes >= 1)
            return $", ~{(int)ts.TotalMinutes}:{ts.Seconds:D2} remaining";
        return $", ~{(int)ts.TotalSeconds}s remaining";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
