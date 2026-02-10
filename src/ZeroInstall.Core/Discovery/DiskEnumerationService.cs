using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Enumerates physical disks and volumes via PowerShell.
/// </summary>
public class DiskEnumerationService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DiskEnumerationService> _logger;

    public DiskEnumerationService(IProcessRunner processRunner, ILogger<DiskEnumerationService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Enumerates all physical disks on the system.
    /// </summary>
    public async Task<List<DiskInfo>> GetDisksAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Enumerating physical disks via PowerShell");

        var result = await _processRunner.RunAsync(
            "powershell",
            "-NoProfile -Command \"Get-Disk | ConvertTo-Json\"",
            ct);

        if (!result.Success)
        {
            _logger.LogWarning("Get-Disk failed (exit code {ExitCode}): {Error}", result.ExitCode, result.StandardError);
            return [];
        }

        return ParseDiskJson(result.StandardOutput);
    }

    /// <summary>
    /// Enumerates all volumes on the system.
    /// </summary>
    public async Task<List<VolumeDetail>> GetVolumesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Enumerating volumes via PowerShell");

        var result = await _processRunner.RunAsync(
            "powershell",
            "-NoProfile -Command \"Get-Volume | Where-Object { $_.DriveLetter -ne $null } | ConvertTo-Json\"",
            ct);

        if (!result.Success)
        {
            _logger.LogWarning("Get-Volume failed (exit code {ExitCode}): {Error}", result.ExitCode, result.StandardError);
            return [];
        }

        return ParseVolumeJson(result.StandardOutput);
    }

    /// <summary>
    /// Parses JSON output from Get-Disk into DiskInfo objects.
    /// Handles PowerShell single-object (not-array) quirk.
    /// </summary>
    internal static List<DiskInfo> ParseDiskJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var disks = new List<DiskInfo>();
                foreach (var element in root.EnumerateArray())
                    disks.Add(ParseSingleDisk(element));
                return disks;
            }

            if (root.ValueKind == JsonValueKind.Object)
                return [ParseSingleDisk(root)];

            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Parses JSON output from Get-Volume into VolumeDetail objects.
    /// Handles PowerShell single-object (not-array) quirk.
    /// </summary>
    internal static List<VolumeDetail> ParseVolumeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var volumes = new List<VolumeDetail>();
                foreach (var element in root.EnumerateArray())
                    volumes.Add(ParseSingleVolume(element));
                return volumes;
            }

            if (root.ValueKind == JsonValueKind.Object)
                return [ParseSingleVolume(root)];

            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DiskInfo ParseSingleDisk(JsonElement element)
    {
        return new DiskInfo
        {
            Number = element.TryGetProperty("Number", out var num) ? num.GetInt32() : 0,
            Model = GetStringProperty(element, "Model"),
            SizeBytes = element.TryGetProperty("Size", out var size) ? size.GetInt64() : 0,
            PartitionStyle = GetPartitionStyleString(element),
            IsOnline = element.TryGetProperty("IsOnline", out var online) && online.GetBoolean(),
            IsSystem = element.TryGetProperty("IsSystem", out var system) && system.GetBoolean(),
            IsBoot = element.TryGetProperty("IsBoot", out var boot) && boot.GetBoolean(),
            BusType = GetBusTypeString(element)
        };
    }

    private static VolumeDetail ParseSingleVolume(JsonElement element)
    {
        return new VolumeDetail
        {
            DriveLetter = GetStringProperty(element, "DriveLetter"),
            Label = GetStringProperty(element, "FileSystemLabel"),
            FileSystem = GetStringProperty(element, "FileSystem"),
            SizeBytes = element.TryGetProperty("Size", out var size) ? size.GetInt64() : 0,
            FreeSpaceBytes = element.TryGetProperty("SizeRemaining", out var free) ? free.GetInt64() : 0,
            DiskNumber = element.TryGetProperty("DiskNumber", out var disk) ? disk.GetInt32() : 0,
            VolumeType = GetStringProperty(element, "DriveType"),
            HealthStatus = GetStringProperty(element, "HealthStatus")
        };
    }

    private static string GetStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return string.Empty;

        return prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : prop.ValueKind == JsonValueKind.Null ? string.Empty : prop.ToString();
    }

    private static string GetPartitionStyleString(JsonElement element)
    {
        if (!element.TryGetProperty("PartitionStyle", out var prop))
            return string.Empty;

        // PowerShell may serialize as int (enum) or string
        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32() switch
            {
                1 => "MBR",
                2 => "GPT",
                _ => "RAW"
            };
        }

        return prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetBusTypeString(JsonElement element)
    {
        if (!element.TryGetProperty("BusType", out var prop))
            return string.Empty;

        // PowerShell may serialize as int (enum) or string
        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32() switch
            {
                3 => "ATA",
                8 => "SATA",
                7 => "USB",
                9 => "SAS",
                11 => "RAID",
                17 => "NVMe",
                _ => "Unknown"
            };
        }

        return prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }
}
