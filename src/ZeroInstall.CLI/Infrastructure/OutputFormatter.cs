using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.CLI.Infrastructure;

/// <summary>
/// Static methods for formatting CLI output as tables or JSON.
/// </summary>
internal static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void WriteDiscoveryResults(IReadOnlyList<MigrationItem> items, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(items, JsonOptions));
            return;
        }

        Console.WriteLine($"{"Type",-15} {"Name",-40} {"Size",10}  {"Tier",-12}");
        Console.WriteLine(new string('-', 80));

        foreach (var item in items)
        {
            Console.WriteLine(
                $"{item.ItemType,-15} {Truncate(item.DisplayName, 40),-40} {FormatBytes(item.EstimatedSizeBytes),10}  {item.RecommendedTier,-12}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {items.Count} items, {FormatBytes(items.Sum(i => i.EstimatedSizeBytes))}");
    }

    public static void WriteJobList(IReadOnlyList<MigrationJob> jobs, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(jobs, JsonOptions));
            return;
        }

        if (jobs.Count == 0)
        {
            Console.WriteLine("No jobs found.");
            return;
        }

        Console.WriteLine($"{"ID",-34} {"Date",-20} {"Status",-14} {"Source -> Dest"}");
        Console.WriteLine(new string('-', 90));

        foreach (var job in jobs)
        {
            var route = $"{Truncate(job.SourceHostname, 15)} -> {Truncate(job.DestinationHostname, 15)}";
            Console.WriteLine(
                $"{Truncate(job.JobId, 32),-34} {job.CreatedUtc:yyyy-MM-dd HH:mm,-20} {job.Status,-14} {route}");
        }
    }

    public static void WriteJobDetail(MigrationJob job, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(job, JsonOptions));
            return;
        }

        Console.WriteLine($"Job ID:       {job.JobId}");
        Console.WriteLine($"Status:       {job.Status}");
        Console.WriteLine($"Created:      {job.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (job.StartedUtc.HasValue)
            Console.WriteLine($"Started:      {job.StartedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (job.CompletedUtc.HasValue)
            Console.WriteLine($"Completed:    {job.CompletedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (job.Duration.HasValue)
            Console.WriteLine($"Duration:     {job.Duration:hh\\:mm\\:ss}");
        Console.WriteLine($"Source:       {job.SourceHostname} ({job.SourceOsVersion})");
        Console.WriteLine($"Destination:  {job.DestinationHostname} ({job.DestinationOsVersion})");
        Console.WriteLine($"Technician:   {job.TechnicianName}");
        Console.WriteLine($"Transport:    {job.TransportMethod}");
        if (!string.IsNullOrEmpty(job.ProfileName))
            Console.WriteLine($"Profile:      {job.ProfileName}");
        Console.WriteLine($"Items:        {job.Items.Count}");
        Console.WriteLine($"User Maps:    {job.UserMappings.Count}");
    }

    public static void WriteProfileList(IReadOnlyList<MigrationProfile> profiles, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(profiles, JsonOptions));
            return;
        }

        if (profiles.Count == 0)
        {
            Console.WriteLine("No profiles found.");
            return;
        }

        Console.WriteLine($"{"Name",-30} {"Description",-35} {"Author",-15}");
        Console.WriteLine(new string('-', 80));

        foreach (var p in profiles)
        {
            Console.WriteLine(
                $"{Truncate(p.Name, 30),-30} {Truncate(p.Description, 35),-35} {Truncate(p.Author, 15),-15}");
        }
    }

    public static void WriteProfileDetail(MigrationProfile profile, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(profile, JsonOptions));
            return;
        }

        Console.WriteLine($"Name:         {profile.Name}");
        Console.WriteLine($"Description:  {profile.Description}");
        Console.WriteLine($"Author:       {profile.Author}");
        Console.WriteLine($"Version:      {profile.Version}");
        Console.WriteLine($"Created:      {profile.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Modified:     {profile.ModifiedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine();
        Console.WriteLine("Item Selection:");
        Console.WriteLine($"  User Profiles:  {(profile.Items.UserProfiles.Enabled ? "Yes" : "No")}");
        Console.WriteLine($"  Applications:   {(profile.Items.Applications.Enabled ? "Yes" : "No")} (Tier: {profile.Items.Applications.PreferredTier})");
        Console.WriteLine($"  Browser Data:   {(profile.Items.BrowserData.Enabled ? "Yes" : "No")}");
        Console.WriteLine($"  System Settings: {(profile.Items.SystemSettings.Enabled ? "Yes" : "No")}");
        Console.WriteLine();
        Console.WriteLine($"Transport:    {profile.Transport.PreferredMethod}");
        if (!string.IsNullOrEmpty(profile.Transport.NasPath))
            Console.WriteLine($"NAS Path:     {profile.Transport.NasPath}");
    }

    public static void WriteReport(JobReport report, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            return;
        }

        Console.WriteLine($"Report ID:    {report.ReportId}");
        Console.WriteLine($"Job ID:       {report.JobId}");
        Console.WriteLine($"Status:       {report.FinalStatus}");
        Console.WriteLine($"Generated:    {report.GeneratedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"  Total:     {report.Summary.TotalItems}");
        Console.WriteLine($"  Completed: {report.Summary.Completed}");
        Console.WriteLine($"  Failed:    {report.Summary.Failed}");
        Console.WriteLine($"  Skipped:   {report.Summary.Skipped}");
        Console.WriteLine($"  Warnings:  {report.Summary.Warnings}");
        Console.WriteLine($"  Transferred: {FormatBytes(report.Summary.TotalBytesTransferred)}");

        if (report.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Errors:");
            foreach (var e in report.Errors)
                Console.WriteLine($"  - {e}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");
            foreach (var w in report.Warnings)
                Console.WriteLine($"  - {w}");
        }
    }

    public static void WriteBitLockerStatus(IReadOnlyList<BitLockerStatus> statuses, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(statuses, JsonOptions));
            return;
        }

        if (statuses.Count == 0)
        {
            Console.WriteLine("No volumes found.");
            return;
        }

        Console.WriteLine($"{"Volume",-10} {"Protection",-15} {"Lock Status",-15} {"Encryption",-20} {"Encrypted %",11}");
        Console.WriteLine(new string('-', 75));

        foreach (var s in statuses)
        {
            Console.WriteLine(
                $"{s.VolumePath,-10} {s.ProtectionStatus,-15} {Truncate(s.LockStatus, 15),-15} {Truncate(s.EncryptionMethod, 20),-20} {s.PercentageEncrypted,10:F1}%");
        }

        Console.WriteLine();

        var encrypted = statuses.Count(s => s.IsEncrypted);
        var locked = statuses.Count(s => s.ProtectionStatus == BitLockerProtectionStatus.Locked);

        Console.WriteLine($"Total: {statuses.Count} volumes, {encrypted} encrypted, {locked} locked");

        if (locked > 0)
        {
            Console.WriteLine();
            Console.WriteLine("WARNING: Locked volumes cannot be cloned. Use 'zim bitlocker unlock <volume>' first.");
        }
    }

    public static void WriteFirmwareInfo(FirmwareInfo info, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(info, JsonOptions));
            return;
        }

        Console.WriteLine("Firmware Information");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"  Firmware Type:      {info.FirmwareType}");
        Console.WriteLine($"  Secure Boot:        {info.SecureBoot}");
        Console.WriteLine($"  TPM Present:        {(info.TpmPresent ? "Yes" : "No")}");
        if (info.TpmPresent)
            Console.WriteLine($"  TPM Version:        {info.TpmVersion}");
        Console.WriteLine($"  BIOS Vendor:        {info.BiosVendor}");
        Console.WriteLine($"  BIOS Version:       {info.BiosVersion}");
        if (!string.IsNullOrEmpty(info.BiosReleaseDate))
            Console.WriteLine($"  BIOS Release Date:  {info.BiosReleaseDate}");
        Console.WriteLine($"  System Manufacturer:{(string.IsNullOrEmpty(info.SystemManufacturer) ? " N/A" : " " + info.SystemManufacturer)}");
        Console.WriteLine($"  System Model:       {(string.IsNullOrEmpty(info.SystemModel) ? "N/A" : info.SystemModel)}");

        if (info.BootEntries.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Boot Entries:       {info.BootEntries.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("NOTE: BIOS/UEFI settings (boot order, virtualization, etc.) are hardware-specific");
        Console.WriteLine("      and cannot be migrated. Configure these manually on the destination machine.");
    }

    public static void WriteBootEntries(IReadOnlyList<BcdBootEntry> entries, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(entries, JsonOptions));
            return;
        }

        if (entries.Count == 0)
        {
            Console.WriteLine("No boot entries found.");
            return;
        }

        Console.WriteLine($"{"Identifier",-40} {"Type",-25} {"Description",-20} {"Default"}");
        Console.WriteLine(new string('-', 95));

        foreach (var e in entries)
        {
            Console.WriteLine(
                $"{Truncate(e.Identifier, 40),-40} {Truncate(e.EntryType, 25),-25} {Truncate(e.Description, 20),-20} {(e.IsDefault ? "*" : "")}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {entries.Count} entries");
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes >= 1_099_511_627_776) return $"{bytes / 1_099_511_627_776.0:F1} TB";
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
