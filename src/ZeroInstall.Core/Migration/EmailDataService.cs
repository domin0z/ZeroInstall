using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Captures and restores email client data (Outlook, Thunderbird) including
/// emails, contacts, calendar, autocomplete, signatures, and account settings.
/// </summary>
public class EmailDataService
{
    private const string ManifestFileName = "email-manifest.json";

    private static readonly string[] ThunderbirdExcludePatterns =
        ["cache2", "startupCache"];

    private static readonly string[] OutlookVersions = ["16.0", "15.0", "14.0"];

    private readonly IFileSystemAccessor _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<EmailDataService> _logger;

    public EmailDataService(
        IFileSystemAccessor fileSystem,
        IProcessRunner processRunner,
        ILogger<EmailDataService> logger)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Captures email client data for selected items.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var emailItems = items
            .Where(i => i.IsSelected && i.ItemType == MigrationItemType.EmailData)
            .ToList();

        if (emailItems.Count == 0) return;

        Directory.CreateDirectory(outputPath);
        var manifest = new EmailCaptureManifest();

        foreach (var item in emailItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.SourceData is not UserProfile profile) continue;

            item.Status = MigrationItemStatus.InProgress;

            foreach (var emailClient in profile.EmailData)
            {
                statusProgress?.Report($"Capturing {emailClient.ClientName} data for {profile.Username}");

                var entries = await CaptureEmailClientAsync(
                    emailClient, profile, outputPath, ct);
                manifest.Entries.AddRange(entries);
            }

            item.Status = MigrationItemStatus.Completed;
        }

        manifest.CapturedUtc = DateTime.UtcNow;

        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Captured {Count} email client entries to {Path}",
            manifest.Entries.Count, outputPath);
    }

    /// <summary>
    /// Restores previously captured email data, applying user mappings.
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(inputPath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("No email manifest found at {Path}", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<EmailCaptureManifest>(json);
        if (manifest is null) return;

        foreach (var entry in manifest.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var mapping = userMappings.FirstOrDefault(m =>
                string.Equals(m.SourceUser.Username, entry.SourceUsername, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                _logger.LogWarning("No user mapping for email data owner {User}", entry.SourceUsername);
                continue;
            }

            statusProgress?.Report($"Restoring {entry.ClientName} for {mapping.DestinationUsername}");

            if (string.Equals(entry.ClientName, "Outlook", StringComparison.OrdinalIgnoreCase))
            {
                await RestoreOutlookAsync(entry, inputPath, mapping, ct);
            }
            else if (string.Equals(entry.ClientName, "Thunderbird", StringComparison.OrdinalIgnoreCase))
            {
                await RestoreThunderbirdAsync(entry, inputPath, mapping, ct);
            }
        }

        _logger.LogInformation("Restored {Count} email client entries from {Path}",
            manifest.Entries.Count, inputPath);
    }

    private async Task<List<CapturedEmailEntry>> CaptureEmailClientAsync(
        EmailClientData emailClient, UserProfile profile, string outputPath, CancellationToken ct)
    {
        var entries = new List<CapturedEmailEntry>();

        if (string.Equals(emailClient.ClientName, "Outlook", StringComparison.OrdinalIgnoreCase))
        {
            var entry = await CaptureOutlookAsync(emailClient, profile, outputPath, ct);
            if (entry is not null) entries.Add(entry);
        }
        else if (string.Equals(emailClient.ClientName, "Thunderbird", StringComparison.OrdinalIgnoreCase))
        {
            var entry = await CaptureThunderbirdAsync(emailClient, profile, outputPath, ct);
            if (entry is not null) entries.Add(entry);
        }

        return entries;
    }

    internal async Task<CapturedEmailEntry?> CaptureOutlookAsync(
        EmailClientData emailClient, UserProfile profile, string outputPath, CancellationToken ct)
    {
        var captureSubDir = $"{profile.Username}_Outlook".Replace(" ", "-");
        var destDir = Path.Combine(outputPath, captureSubDir);

        try
        {
            Directory.CreateDirectory(destDir);
            var components = new List<CapturedEmailComponent>();

            // 1. Copy PST files
            foreach (var dataPath in emailClient.DataPaths)
            {
                if (_fileSystem.FileExists(dataPath) &&
                    dataPath.EndsWith(".pst", StringComparison.OrdinalIgnoreCase))
                {
                    var pstDir = Path.Combine(destDir, "PST");
                    Directory.CreateDirectory(pstDir);
                    var destFile = Path.Combine(pstDir, Path.GetFileName(dataPath));
                    File.Copy(dataPath, destFile, overwrite: true);
                    components.Add(new CapturedEmailComponent
                    {
                        ComponentType = EmailComponentType.PST,
                        RelativePath = Path.Combine("PST", Path.GetFileName(dataPath))
                    });
                }
            }

            // 2. Copy OST files (Exchange cache, will rebuild)
            foreach (var dataPath in emailClient.DataPaths)
            {
                if (_fileSystem.FileExists(dataPath) &&
                    dataPath.EndsWith(".ost", StringComparison.OrdinalIgnoreCase))
                {
                    var ostDir = Path.Combine(destDir, "OST");
                    Directory.CreateDirectory(ostDir);
                    var destFile = Path.Combine(ostDir, Path.GetFileName(dataPath));
                    File.Copy(dataPath, destFile, overwrite: true);
                    components.Add(new CapturedEmailComponent
                    {
                        ComponentType = EmailComponentType.OST,
                        RelativePath = Path.Combine("OST", Path.GetFileName(dataPath)),
                        Note = "Exchange cache file — will rebuild on destination"
                    });
                }
            }

            // 3. Copy signatures
            var signaturesPath = Path.Combine(profile.ProfilePath, "AppData", "Roaming", "Microsoft", "Signatures");
            if (_fileSystem.DirectoryExists(signaturesPath))
            {
                var sigDest = Path.Combine(destDir, "Signatures");
                ProfileTransferService.CopyDirectoryWithTimestamps(signaturesPath, sigDest);
                components.Add(new CapturedEmailComponent
                {
                    ComponentType = EmailComponentType.Signatures,
                    RelativePath = "Signatures"
                });
            }

            // 4. Copy autocomplete (RoamCache)
            var roamCachePath = Path.Combine(profile.ProfilePath, "AppData", "Local", "Microsoft", "Outlook", "RoamCache");
            if (_fileSystem.DirectoryExists(roamCachePath))
            {
                var roamDest = Path.Combine(destDir, "RoamCache");
                ProfileTransferService.CopyDirectoryWithTimestamps(roamCachePath, roamDest);
                components.Add(new CapturedEmailComponent
                {
                    ComponentType = EmailComponentType.Autocomplete,
                    RelativePath = "RoamCache"
                });
            }

            // 5. Copy custom templates (.oft)
            var templatesPath = Path.Combine(profile.ProfilePath, "AppData", "Roaming", "Microsoft", "Templates");
            if (_fileSystem.DirectoryExists(templatesPath))
            {
                var oftFiles = _fileSystem.GetFiles(templatesPath, "*.oft");
                if (oftFiles.Length > 0)
                {
                    var templatesDest = Path.Combine(destDir, "Templates");
                    Directory.CreateDirectory(templatesDest);
                    foreach (var oft in oftFiles)
                    {
                        var destFile = Path.Combine(templatesDest, Path.GetFileName(oft));
                        File.Copy(oft, destFile, overwrite: true);
                    }
                    components.Add(new CapturedEmailComponent
                    {
                        ComponentType = EmailComponentType.Templates,
                        RelativePath = "Templates"
                    });
                }
            }

            // 6. Export Outlook profile registry
            await ExportOutlookRegistryAsync(destDir, components, ct);

            // 7. Log DPAPI password warning
            _logger.LogWarning(
                "Outlook: account passwords are DPAPI-encrypted and cannot transfer between machines. " +
                "User will need to re-enter passwords, but account configuration will be preserved.");

            return new CapturedEmailEntry
            {
                ClientName = "Outlook",
                SourceUsername = profile.Username,
                CaptureSubDir = captureSubDir,
                Components = components
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture Outlook data for {User}", profile.Username);
            return null;
        }
    }

    internal async Task ExportOutlookRegistryAsync(
        string destDir, List<CapturedEmailComponent> components, CancellationToken ct)
    {
        var regDir = Path.Combine(destDir, "Registry");
        Directory.CreateDirectory(regDir);

        foreach (var version in OutlookVersions)
        {
            var regKey = $@"HKCU\Software\Microsoft\Office\{version}\Outlook";
            var regFile = Path.Combine(regDir, $"outlook-{version.Replace(".", "")}.reg");

            var result = await _processRunner.RunAsync(
                "reg", $"export \"{regKey}\" \"{regFile}\" /y", ct);

            if (result.Success)
            {
                components.Add(new CapturedEmailComponent
                {
                    ComponentType = EmailComponentType.Registry,
                    RelativePath = Path.Combine("Registry", $"outlook-{version.Replace(".", "")}.reg"),
                    Note = $"Outlook {version} registry profile"
                });

                _logger.LogDebug("Exported Outlook {Version} registry to {File}", version, regFile);
            }
        }
    }

    internal async Task<CapturedEmailEntry?> CaptureThunderbirdAsync(
        EmailClientData emailClient, UserProfile profile, string outputPath, CancellationToken ct)
    {
        var captureSubDir = $"{profile.Username}_Thunderbird".Replace(" ", "-");
        var destDir = Path.Combine(outputPath, captureSubDir);

        try
        {
            Directory.CreateDirectory(destDir);
            var components = new List<CapturedEmailComponent>();

            // 1. Copy profile directories (preserves key4.db + logins.json = passwords transfer)
            foreach (var dataPath in emailClient.DataPaths)
            {
                if (!_fileSystem.DirectoryExists(dataPath)) continue;

                var profileDirName = Path.GetFileName(dataPath);
                var profileDest = Path.Combine(destDir, "Profiles", profileDirName);
                BrowserDataService.CopyDirectorySelective(dataPath, profileDest, ThunderbirdExcludePatterns);

                components.Add(new CapturedEmailComponent
                {
                    ComponentType = EmailComponentType.Profile,
                    RelativePath = Path.Combine("Profiles", profileDirName)
                });
            }

            // 2. Copy profiles.ini
            var profilesIniPath = Path.Combine(
                profile.ProfilePath, "AppData", "Roaming", "Thunderbird", "profiles.ini");
            if (_fileSystem.FileExists(profilesIniPath))
            {
                var iniDest = Path.Combine(destDir, "profiles.ini");
                File.Copy(profilesIniPath, iniDest, overwrite: true);
                components.Add(new CapturedEmailComponent
                {
                    ComponentType = EmailComponentType.Profile,
                    RelativePath = "profiles.ini",
                    Note = "Thunderbird profiles.ini"
                });
            }

            await Task.CompletedTask;

            return new CapturedEmailEntry
            {
                ClientName = "Thunderbird",
                SourceUsername = profile.Username,
                CaptureSubDir = captureSubDir,
                Components = components
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture Thunderbird data for {User}", profile.Username);
            return null;
        }
    }

    internal async Task RestoreOutlookAsync(
        CapturedEmailEntry entry, string inputPath, UserMapping mapping, CancellationToken ct)
    {
        var capturedDir = Path.Combine(inputPath, entry.CaptureSubDir);
        if (!Directory.Exists(capturedDir)) return;

        var destProfile = mapping.DestinationProfilePath;

        // 1. Restore PST files
        var pstDir = Path.Combine(capturedDir, "PST");
        if (Directory.Exists(pstDir))
        {
            var destPstDir = Path.Combine(destProfile, "Documents", "Outlook Files");
            Directory.CreateDirectory(destPstDir);
            foreach (var pstFile in Directory.GetFiles(pstDir, "*.pst"))
            {
                File.Copy(pstFile, Path.Combine(destPstDir, Path.GetFileName(pstFile)), overwrite: true);
            }
        }

        // 2. Restore signatures
        var sigDir = Path.Combine(capturedDir, "Signatures");
        if (Directory.Exists(sigDir))
        {
            var destSigDir = Path.Combine(destProfile, "AppData", "Roaming", "Microsoft", "Signatures");
            ProfileTransferService.CopyDirectoryWithTimestamps(sigDir, destSigDir);
        }

        // 3. Restore autocomplete (RoamCache)
        var roamDir = Path.Combine(capturedDir, "RoamCache");
        if (Directory.Exists(roamDir))
        {
            var destRoamDir = Path.Combine(destProfile, "AppData", "Local", "Microsoft", "Outlook", "RoamCache");
            ProfileTransferService.CopyDirectoryWithTimestamps(roamDir, destRoamDir);
        }

        // 4. Restore templates
        var templatesDir = Path.Combine(capturedDir, "Templates");
        if (Directory.Exists(templatesDir))
        {
            var destTemplatesDir = Path.Combine(destProfile, "AppData", "Roaming", "Microsoft", "Templates");
            Directory.CreateDirectory(destTemplatesDir);
            foreach (var oft in Directory.GetFiles(templatesDir, "*.oft"))
            {
                File.Copy(oft, Path.Combine(destTemplatesDir, Path.GetFileName(oft)), overwrite: true);
            }
        }

        // 5. Import registry with path remapping
        await ImportOutlookRegistryAsync(capturedDir, mapping, ct);

        _logger.LogWarning(
            "Outlook restored for {User}. Account passwords are DPAPI-protected and must be re-entered.",
            mapping.DestinationUsername);
    }

    internal async Task ImportOutlookRegistryAsync(
        string capturedDir, UserMapping mapping, CancellationToken ct)
    {
        var regDir = Path.Combine(capturedDir, "Registry");
        if (!Directory.Exists(regDir)) return;

        foreach (var regFile in Directory.GetFiles(regDir, "*.reg"))
        {
            // Read and remap paths in the .reg file
            var content = await File.ReadAllTextAsync(regFile, ct);

            if (mapping.RequiresPathRemapping)
            {
                // .reg files use double-backslash paths
                var sourcePrefix = mapping.SourcePathPrefix.Replace(@"\", @"\\");
                var destPrefix = mapping.DestinationProfilePath.Replace(@"\", @"\\");
                content = content.Replace(sourcePrefix, destPrefix, StringComparison.OrdinalIgnoreCase);

                // Also remap single-backslash paths (some string values)
                content = content.Replace(mapping.SourcePathPrefix, mapping.DestinationProfilePath,
                    StringComparison.OrdinalIgnoreCase);
            }

            var remappedFile = regFile + ".remapped.reg";
            await File.WriteAllTextAsync(remappedFile, content, ct);

            var result = await _processRunner.RunAsync("reg", $"import \"{remappedFile}\"", ct);
            if (!result.Success)
            {
                _logger.LogWarning("Failed to import Outlook registry file {File}: {Error}",
                    regFile, result.StandardError);
            }
        }
    }

    internal async Task RestoreThunderbirdAsync(
        CapturedEmailEntry entry, string inputPath, UserMapping mapping, CancellationToken ct)
    {
        var capturedDir = Path.Combine(inputPath, entry.CaptureSubDir);
        if (!Directory.Exists(capturedDir)) return;

        var destProfile = mapping.DestinationProfilePath;

        // 1. Copy profiles to destination
        var profilesDir = Path.Combine(capturedDir, "Profiles");
        if (Directory.Exists(profilesDir))
        {
            var destProfilesDir = Path.Combine(
                destProfile, "AppData", "Roaming", "Thunderbird", "Profiles");
            ProfileTransferService.CopyDirectoryWithTimestamps(profilesDir, destProfilesDir);
        }

        // 2. Write updated profiles.ini pointing to new profile locations
        var capturedIni = Path.Combine(capturedDir, "profiles.ini");
        if (File.Exists(capturedIni))
        {
            await WriteThunderbirdProfilesIniAsync(capturedIni, destProfile, mapping, ct);
        }

        _logger.LogDebug("Restored Thunderbird data for {User}. Passwords preserved via key4.db.",
            mapping.DestinationUsername);
    }

    internal async Task WriteThunderbirdProfilesIniAsync(
        string capturedIniPath, string destProfilePath, UserMapping mapping, CancellationToken ct)
    {
        var destThunderbirdDir = Path.Combine(
            destProfilePath, "AppData", "Roaming", "Thunderbird");
        Directory.CreateDirectory(destThunderbirdDir);

        var content = await File.ReadAllTextAsync(capturedIniPath, ct);

        // Remap profile paths if the username changed
        if (mapping.RequiresPathRemapping)
        {
            content = content.Replace(mapping.SourcePathPrefix, mapping.DestinationProfilePath,
                StringComparison.OrdinalIgnoreCase);
        }

        var destIniPath = Path.Combine(destThunderbirdDir, "profiles.ini");
        await File.WriteAllTextAsync(destIniPath, content, ct);

        _logger.LogDebug("Updated Thunderbird profiles.ini for {User}", mapping.DestinationUsername);
    }
}

/// <summary>
/// Manifest tracking captured email client data.
/// </summary>
public class EmailCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<CapturedEmailEntry> Entries { get; set; } = [];
}

/// <summary>
/// A captured email client entry.
/// </summary>
public class CapturedEmailEntry
{
    public string ClientName { get; set; } = string.Empty;
    public string SourceUsername { get; set; } = string.Empty;
    public string CaptureSubDir { get; set; } = string.Empty;
    public List<CapturedEmailComponent> Components { get; set; } = [];
}

/// <summary>
/// A component captured within an email client entry.
/// </summary>
public class CapturedEmailComponent
{
    public EmailComponentType ComponentType { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>
/// Types of email data components that can be captured.
/// </summary>
public enum EmailComponentType
{
    PST,
    OST,
    Signatures,
    Autocomplete,
    Templates,
    Registry,
    Profile
}
