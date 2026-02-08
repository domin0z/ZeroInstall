using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Copies user profile known folders (Documents, Desktop, Downloads, etc.)
/// with manifest tracking and ACL re-permissioning on restore.
/// </summary>
public class ProfileTransferService
{
    private const string ManifestFileName = "profile-transfer-manifest.json";

    private static readonly string[] KnownFolderNames =
        ["Documents", "Desktop", "Downloads", "Pictures", "Music", "Videos", "Favorites"];

    private readonly IFileSystemAccessor _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<ProfileTransferService> _logger;

    public ProfileTransferService(
        IFileSystemAccessor fileSystem,
        IProcessRunner processRunner,
        ILogger<ProfileTransferService> logger)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Captures user profile folders for selected items and writes a manifest.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<string>? statusProgress = null,
        CancellationToken ct = default)
    {
        var profileItems = items
            .Where(i => i.IsSelected && i.ItemType == MigrationItemType.UserProfile)
            .ToList();

        if (profileItems.Count == 0) return;

        Directory.CreateDirectory(outputPath);

        var manifest = new ProfileTransferManifest();

        foreach (var item in profileItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.SourceData is not UserProfile profile) continue;

            item.Status = MigrationItemStatus.InProgress;
            statusProgress?.Report($"Capturing profile: {profile.Username}");

            var entry = await CaptureUserProfileAsync(profile, outputPath, ct);
            manifest.Profiles.Add(entry);

            item.Status = MigrationItemStatus.Completed;
        }

        manifest.CapturedUtc = DateTime.UtcNow;

        var manifestPath = Path.Combine(outputPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Captured {Count} user profiles to {Path}",
            manifest.Profiles.Count, outputPath);
    }

    /// <summary>
    /// Restores user profile folders from a previous capture, applying user mappings.
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
            _logger.LogWarning("No profile transfer manifest found at {Path}", manifestPath);
            return;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<ProfileTransferManifest>(json);
        if (manifest is null) return;

        foreach (var profile in manifest.Profiles)
        {
            ct.ThrowIfCancellationRequested();

            var mapping = userMappings.FirstOrDefault(m =>
                string.Equals(m.SourceUser.Username, profile.Username, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                _logger.LogWarning("No user mapping found for profile {Username}, skipping", profile.Username);
                continue;
            }

            statusProgress?.Report($"Restoring profile: {profile.Username} → {mapping.DestinationUsername}");

            foreach (var folder in profile.Folders)
            {
                var sourceCaptureDir = Path.Combine(inputPath, profile.Username, folder.FolderName);
                if (!Directory.Exists(sourceCaptureDir)) continue;

                var destFolder = Path.Combine(mapping.DestinationProfilePath, folder.FolderName);
                CopyDirectoryWithTimestamps(sourceCaptureDir, destFolder);

                _logger.LogDebug("Restored {Folder} for {User}", folder.FolderName, mapping.DestinationUsername);
            }

            // Re-ACL the profile directory for the destination user
            if (!string.IsNullOrEmpty(mapping.DestinationSid))
            {
                await ReAclProfileDirectoryAsync(
                    mapping.DestinationProfilePath,
                    mapping.DestinationSid,
                    mapping.DestinationUsername, ct);
            }
        }

        _logger.LogInformation("Restored {Count} user profiles from {Path}",
            manifest.Profiles.Count, inputPath);
    }

    private async Task<CapturedProfileEntry> CaptureUserProfileAsync(
        UserProfile profile, string outputPath, CancellationToken ct)
    {
        var entry = new CapturedProfileEntry
        {
            Username = profile.Username,
            SourceProfilePath = profile.ProfilePath
        };

        var folders = GetKnownFolderPaths(profile);
        var userOutputDir = Path.Combine(outputPath, profile.Username);

        foreach (var (folderName, folderPath) in folders)
        {
            if (string.IsNullOrEmpty(folderPath) || !_fileSystem.DirectoryExists(folderPath))
                continue;

            var destDir = Path.Combine(userOutputDir, folderName);

            try
            {
                CopyDirectoryWithTimestamps(folderPath, destDir);
                var size = _fileSystem.GetDirectorySize(folderPath);

                entry.Folders.Add(new CapturedFolderEntry
                {
                    FolderName = folderName,
                    OriginalPath = folderPath,
                    SizeBytes = size
                });

                _logger.LogDebug("Captured {Folder} for user {User} ({Size} bytes)",
                    folderName, profile.Username, size);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture {Folder} for user {User}",
                    folderName, profile.Username);
            }
        }

        await Task.CompletedTask;
        return entry;
    }

    internal async Task ReAclProfileDirectoryAsync(
        string directory, string sid, string username, CancellationToken ct = default)
    {
        // Grant full control to the destination user
        var grantResult = await _processRunner.RunAsync(
            "icacls", $"\"{directory}\" /grant \"{username}:(OI)(CI)F\" /T /Q", ct);

        if (!grantResult.Success)
        {
            _logger.LogWarning("Failed to grant ACL for {User} on {Dir}: {Error}",
                username, directory, grantResult.StandardError);
        }

        // Set owner
        var ownerResult = await _processRunner.RunAsync(
            "icacls", $"\"{directory}\" /setowner \"{username}\" /T /Q", ct);

        if (!ownerResult.Success)
        {
            _logger.LogWarning("Failed to set owner for {User} on {Dir}: {Error}",
                username, directory, ownerResult.StandardError);
        }
    }

    internal static void CopyDirectoryWithTimestamps(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);

            // Preserve timestamps
            var sourceInfo = new FileInfo(file);
            var destInfo = new FileInfo(destFile);
            destInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
            destInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
            destInfo.LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc;
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryWithTimestamps(dir, destSubDir);
        }

        // Preserve directory timestamps after all contents are copied
        var srcDirInfo = new DirectoryInfo(sourceDir);
        var destDirInfo = new DirectoryInfo(destDir);
        destDirInfo.CreationTimeUtc = srcDirInfo.CreationTimeUtc;
        destDirInfo.LastWriteTimeUtc = srcDirInfo.LastWriteTimeUtc;
    }

    private static List<(string Name, string? Path)> GetKnownFolderPaths(UserProfile profile)
    {
        return
        [
            ("Documents", profile.Folders.Documents),
            ("Desktop", profile.Folders.Desktop),
            ("Downloads", profile.Folders.Downloads),
            ("Pictures", profile.Folders.Pictures),
            ("Music", profile.Folders.Music),
            ("Videos", profile.Folders.Videos),
            ("Favorites", profile.Folders.Favorites)
        ];
    }
}

/// <summary>
/// Manifest tracking captured user profile data.
/// </summary>
public class ProfileTransferManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<CapturedProfileEntry> Profiles { get; set; } = [];
}

/// <summary>
/// A captured user profile with its folders.
/// </summary>
public class CapturedProfileEntry
{
    public string Username { get; set; } = string.Empty;
    public string SourceProfilePath { get; set; } = string.Empty;
    public List<CapturedFolderEntry> Folders { get; set; } = [];
}

/// <summary>
/// A captured known folder within a user profile.
/// </summary>
public class CapturedFolderEntry
{
    public string FolderName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
