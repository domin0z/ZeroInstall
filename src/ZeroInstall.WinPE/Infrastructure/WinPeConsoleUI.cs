using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.WinPE.Infrastructure;

/// <summary>
/// Console UI helpers for the WinPE restore environment.
/// </summary>
internal static class WinPeConsoleUI
{
    public static void WriteHeader()
    {
        Console.WriteLine();
        Console.WriteLine("  ZeroInstall WinPE Restore (zim-winpe)");
        Console.WriteLine("  ======================================");
        Console.WriteLine("  Full disk image restore with driver injection");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays a numbered menu and returns the user's selection (0-based index).
    /// </summary>
    public static int ShowMenu(string title, string[] options)
    {
        Console.WriteLine();
        Console.WriteLine($"  {title}");
        Console.WriteLine($"  {new string('-', title.Length)}");

        for (int i = 0; i < options.Length; i++)
            Console.WriteLine($"  [{i + 1}] {options[i]}");

        while (true)
        {
            Console.Write($"  Select (1-{options.Length}): ");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= options.Length)
                return choice - 1;

            Console.WriteLine("  Invalid selection, try again.");
        }
    }

    /// <summary>
    /// Prompts the user for a yes/no confirmation.
    /// </summary>
    public static bool PromptYesNo(string prompt)
    {
        Console.Write($"  {prompt} (y/n): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return input == "y" || input == "yes";
    }

    /// <summary>
    /// Displays a formatted table of physical disks.
    /// </summary>
    public static void ShowDiskTable(List<DiskInfo> disks)
    {
        Console.WriteLine();
        Console.WriteLine("  {0,-5} {1,-30} {2,12} {3,-6} {4,-6} {5,-8}",
            "Disk", "Model", "Size", "Style", "Online", "Bus");
        Console.WriteLine("  {0,-5} {1,-30} {2,12} {3,-6} {4,-6} {5,-8}",
            "----", "-----", "----", "-----", "------", "---");

        foreach (var disk in disks)
        {
            Console.WriteLine("  {0,-5} {1,-30} {2,12} {3,-6} {4,-6} {5,-8}",
                disk.Number,
                Truncate(disk.Model, 30),
                FormatBytes(disk.SizeBytes),
                disk.PartitionStyle,
                disk.IsOnline ? "Yes" : "No",
                disk.BusType);
        }
    }

    /// <summary>
    /// Displays a formatted table of volumes.
    /// </summary>
    public static void ShowVolumeTable(List<VolumeDetail> volumes)
    {
        Console.WriteLine();
        Console.WriteLine("  {0,-6} {1,-15} {2,-8} {3,12} {4,12} {5,-5} {6,-10}",
            "Drive", "Label", "FS", "Size", "Free", "Disk", "Health");
        Console.WriteLine("  {0,-6} {1,-15} {2,-8} {3,12} {4,12} {5,-5} {6,-10}",
            "-----", "-----", "--", "----", "----", "----", "------");

        foreach (var vol in volumes)
        {
            Console.WriteLine("  {0,-6} {1,-15} {2,-8} {3,12} {4,12} {5,-5} {6,-10}",
                string.IsNullOrEmpty(vol.DriveLetter) ? "-" : $"{vol.DriveLetter}:",
                Truncate(vol.Label, 15),
                vol.FileSystem,
                FormatBytes(vol.SizeBytes),
                FormatBytes(vol.FreeSpaceBytes),
                vol.DiskNumber,
                vol.HealthStatus);
        }
    }

    /// <summary>
    /// Displays metadata for a disk image.
    /// </summary>
    public static void ShowImageInfo(DiskImageMetadata metadata)
    {
        Console.WriteLine();
        Console.WriteLine("  Image Metadata");
        Console.WriteLine("  --------------");
        Console.WriteLine($"  Source Host:    {metadata.SourceHostname}");
        Console.WriteLine($"  Source OS:      {metadata.SourceOsVersion}");
        Console.WriteLine($"  Source Volume:  {metadata.SourceVolume}");
        Console.WriteLine($"  Volume Size:    {FormatBytes(metadata.SourceVolumeSizeBytes)}");
        Console.WriteLine($"  Used Space:     {FormatBytes(metadata.SourceVolumeUsedBytes)}");
        Console.WriteLine($"  Image Size:     {FormatBytes(metadata.ImageSizeBytes)}");
        Console.WriteLine($"  Format:         {metadata.Format}");
        Console.WriteLine($"  Compressed:     {(metadata.IsCompressed ? "Yes" : "No")}");
        Console.WriteLine($"  Split:          {(metadata.IsSplit ? $"Yes ({metadata.ChunkCount} chunks)" : "No")}");
        Console.WriteLine($"  Captured:       {metadata.CapturedUtc:yyyy-MM-dd HH:mm:ss} UTC");

        if (!string.IsNullOrEmpty(metadata.Checksum))
            Console.WriteLine($"  Checksum:       {metadata.Checksum[..Math.Min(16, metadata.Checksum.Length)]}...");
    }

    /// <summary>
    /// Writes a progress bar for transfer operations.
    /// </summary>
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

        var line = $"\r  [{bar}] {percent,5:F1}% | {speed,10} | ETA: {eta,-12}";
        Console.Write(line);

        if (Math.Abs(progress.OverallPercentage - 1.0) < 0.001)
            Console.WriteLine();
    }

    public static void WriteSuccess(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [OK] {message}");
        Console.ForegroundColor = prev;
    }

    public static void WriteError(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERROR] {message}");
        Console.ForegroundColor = prev;
    }

    public static void WriteWarning(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [WARN] {message}");
        Console.ForegroundColor = prev;
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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
