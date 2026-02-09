using ZeroInstall.Agent.Models;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Agent.Infrastructure;

/// <summary>
/// Console output helpers for portable mode.
/// </summary>
internal static class AgentConsoleUI
{
    public static void WriteHeader(AgentOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("  ZeroInstall Transfer Agent (zim-agent)");
        Console.WriteLine("  =======================================");
        Console.WriteLine($"  Role:      {options.Role}");
        Console.WriteLine($"  Port:      {options.Port}");
        Console.WriteLine($"  Directory: {options.DirectoryPath}");
        if (!string.IsNullOrEmpty(options.PeerAddress))
            Console.WriteLine($"  Peer:      {options.PeerAddress}");
        Console.WriteLine();
    }

    public static void WriteStatus(string status)
    {
        Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {status}");
    }

    public static void WriteProgress(TransferProgress progress)
    {
        var percent = progress.OverallPercentage * 100;
        var barLength = 30;
        var filled = (int)(barLength * progress.OverallPercentage);
        var bar = new string('#', filled) + new string('-', barLength - filled);

        var speed = FormatBytes(progress.BytesPerSecond) + "/s";
        var eta = progress.EstimatedTimeRemaining.HasValue
            ? FormatTimeSpan(progress.EstimatedTimeRemaining.Value)
            : "calculating...";

        var line = $"\r  [{bar}] {percent,5:F1}% | {speed,10} | ETA: {eta,-12} | {progress.CurrentItemIndex}/{progress.TotalItems}";
        Console.Write(line);

        if (Math.Abs(progress.OverallPercentage - 1.0) < 0.001)
            Console.WriteLine();
    }

    public static void WriteConnectionInfo(string hostname)
    {
        Console.WriteLine($"  Connected to: {hostname}");
    }

    public static void WriteSummary(int fileCount, long totalBytes, TimeSpan duration)
    {
        Console.WriteLine();
        Console.WriteLine("  Transfer Summary");
        Console.WriteLine("  ----------------");
        Console.WriteLine($"  Files transferred: {fileCount}");
        Console.WriteLine($"  Total size:        {FormatBytes(totalBytes)}");
        Console.WriteLine($"  Duration:          {FormatTimeSpan(duration)}");
        if (duration.TotalSeconds > 0)
            Console.WriteLine($"  Average speed:     {FormatBytes((long)(totalBytes / duration.TotalSeconds))}/s");
        Console.WriteLine();
    }

    internal static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):F2} MB",
            >= 1024L => $"{bytes / 1024.0:F2} KB",
            _ => $"{bytes} B"
        };
    }

    internal static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
        return $"{(int)ts.TotalSeconds}s";
    }
}
