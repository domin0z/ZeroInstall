using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers local user profiles, their known folders, browser profiles, and email client data.
/// </summary>
public class UserProfileDiscoveryService
{
    private const string ProfileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    private readonly IRegistryAccessor _registry;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger<UserProfileDiscoveryService> _logger;

    public UserProfileDiscoveryService(
        IRegistryAccessor registry,
        IFileSystemAccessor fileSystem,
        ILogger<UserProfileDiscoveryService> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Discovers all user profiles on the machine.
    /// </summary>
    public Task<List<UserProfile>> DiscoverAsync(CancellationToken ct = default)
    {
        var profiles = new List<UserProfile>();

        string[] sids;
        try
        {
            sids = _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ProfileListKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read profile list from registry");
            return Task.FromResult(profiles);
        }

        foreach (var sid in sids)
        {
            ct.ThrowIfCancellationRequested();

            // Skip system/service SIDs (S-1-5-18, S-1-5-19, S-1-5-20, etc.)
            if (!sid.StartsWith("S-1-5-21-"))
                continue;

            var profilePath = _registry.GetStringValue(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                $@"{ProfileListKey}\{sid}", "ProfileImagePath");

            if (string.IsNullOrEmpty(profilePath) || !_fileSystem.DirectoryExists(profilePath))
                continue;

            var username = Path.GetFileName(profilePath);

            var profile = new UserProfile
            {
                Username = username,
                Sid = sid,
                ProfilePath = profilePath,
                IsLocal = !sid.Contains("-500"), // Rough heuristic; can be refined
                Folders = DiscoverFolders(profilePath),
                BrowserProfiles = DiscoverBrowserProfiles(profilePath),
                EmailData = DiscoverEmailData(profilePath)
            };

            profile.EstimatedSizeBytes = CalculateProfileSize(profile);

            profiles.Add(profile);
            _logger.LogInformation("Discovered user profile: {Username} ({Sid})", username, sid);
        }

        return Task.FromResult(profiles);
    }

    internal UserProfileFolders DiscoverFolders(string profilePath)
    {
        return new UserProfileFolders
        {
            Documents = ResolveFolderPath(profilePath, "Documents"),
            Desktop = ResolveFolderPath(profilePath, "Desktop"),
            Downloads = ResolveFolderPath(profilePath, "Downloads"),
            Pictures = ResolveFolderPath(profilePath, "Pictures"),
            Music = ResolveFolderPath(profilePath, "Music"),
            Videos = ResolveFolderPath(profilePath, "Videos"),
            Favorites = ResolveFolderPath(profilePath, "Favorites"),
            AppDataRoaming = ResolveFolderPath(profilePath, @"AppData\Roaming"),
            AppDataLocal = ResolveFolderPath(profilePath, @"AppData\Local"),
            AppDataLocalLow = ResolveFolderPath(profilePath, @"AppData\LocalLow")
        };
    }

    private string? ResolveFolderPath(string profilePath, string relativePath)
    {
        var fullPath = Path.Combine(profilePath, relativePath);
        return _fileSystem.DirectoryExists(fullPath) ? fullPath : null;
    }

    internal List<BrowserProfile> DiscoverBrowserProfiles(string profilePath)
    {
        var browsers = new List<BrowserProfile>();

        // Google Chrome
        var chromeBase = Path.Combine(profilePath, @"AppData\Local\Google\Chrome\User Data");
        if (_fileSystem.DirectoryExists(chromeBase))
        {
            browsers.AddRange(DiscoverChromeProfiles(chromeBase));
        }

        // Mozilla Firefox
        var firefoxBase = Path.Combine(profilePath, @"AppData\Roaming\Mozilla\Firefox\Profiles");
        if (_fileSystem.DirectoryExists(firefoxBase))
        {
            browsers.AddRange(DiscoverFirefoxProfiles(firefoxBase));
        }

        // Microsoft Edge
        var edgeBase = Path.Combine(profilePath, @"AppData\Local\Microsoft\Edge\User Data");
        if (_fileSystem.DirectoryExists(edgeBase))
        {
            browsers.AddRange(DiscoverEdgeProfiles(edgeBase));
        }

        return browsers;
    }

    private List<BrowserProfile> DiscoverChromeProfiles(string chromeBase)
    {
        var profiles = new List<BrowserProfile>();

        // Default profile
        var defaultProfile = Path.Combine(chromeBase, "Default");
        if (_fileSystem.DirectoryExists(defaultProfile))
        {
            profiles.Add(new BrowserProfile
            {
                BrowserName = "Google Chrome",
                ProfilePath = defaultProfile,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(defaultProfile)
            });
        }

        // Additional profiles (Profile 1, Profile 2, etc.)
        try
        {
            foreach (var dir in _fileSystem.GetDirectories(chromeBase))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                {
                    profiles.Add(new BrowserProfile
                    {
                        BrowserName = "Google Chrome",
                        ProfilePath = dir,
                        ProfileName = dirName,
                        EstimatedSizeBytes = _fileSystem.GetDirectorySize(dir)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate Chrome profiles");
        }

        return profiles;
    }

    private List<BrowserProfile> DiscoverFirefoxProfiles(string firefoxBase)
    {
        var profiles = new List<BrowserProfile>();

        try
        {
            foreach (var dir in _fileSystem.GetDirectories(firefoxBase))
            {
                profiles.Add(new BrowserProfile
                {
                    BrowserName = "Mozilla Firefox",
                    ProfilePath = dir,
                    ProfileName = Path.GetFileName(dir),
                    EstimatedSizeBytes = _fileSystem.GetDirectorySize(dir)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate Firefox profiles");
        }

        return profiles;
    }

    private List<BrowserProfile> DiscoverEdgeProfiles(string edgeBase)
    {
        var profiles = new List<BrowserProfile>();

        var defaultProfile = Path.Combine(edgeBase, "Default");
        if (_fileSystem.DirectoryExists(defaultProfile))
        {
            profiles.Add(new BrowserProfile
            {
                BrowserName = "Microsoft Edge",
                ProfilePath = defaultProfile,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(defaultProfile)
            });
        }

        try
        {
            foreach (var dir in _fileSystem.GetDirectories(edgeBase))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                {
                    profiles.Add(new BrowserProfile
                    {
                        BrowserName = "Microsoft Edge",
                        ProfilePath = dir,
                        ProfileName = dirName,
                        EstimatedSizeBytes = _fileSystem.GetDirectorySize(dir)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate Edge profiles");
        }

        return profiles;
    }

    internal List<EmailClientData> DiscoverEmailData(string profilePath)
    {
        var emailData = new List<EmailClientData>();

        // Outlook PST/OST files
        var outlookPaths = new[]
        {
            Path.Combine(profilePath, @"Documents\Outlook Files"),
            Path.Combine(profilePath, @"AppData\Local\Microsoft\Outlook")
        };

        var outlookFiles = new List<string>();
        foreach (var path in outlookPaths)
        {
            if (!_fileSystem.DirectoryExists(path)) continue;

            var pstFiles = _fileSystem.GetFiles(path, "*.pst");
            var ostFiles = _fileSystem.GetFiles(path, "*.ost");
            outlookFiles.AddRange(pstFiles);
            outlookFiles.AddRange(ostFiles);
        }

        if (outlookFiles.Count > 0)
        {
            emailData.Add(new EmailClientData
            {
                ClientName = "Microsoft Outlook",
                DataPaths = outlookFiles,
                EstimatedSizeBytes = outlookFiles.Sum(f => _fileSystem.GetFileSize(f))
            });
        }

        // Thunderbird profiles
        var thunderbirdBase = Path.Combine(profilePath, @"AppData\Roaming\Thunderbird\Profiles");
        if (_fileSystem.DirectoryExists(thunderbirdBase))
        {
            var tbDirs = _fileSystem.GetDirectories(thunderbirdBase);
            if (tbDirs.Length > 0)
            {
                emailData.Add(new EmailClientData
                {
                    ClientName = "Mozilla Thunderbird",
                    DataPaths = tbDirs.ToList(),
                    EstimatedSizeBytes = tbDirs.Sum(d => _fileSystem.GetDirectorySize(d))
                });
            }
        }

        return emailData;
    }

    private long CalculateProfileSize(UserProfile profile)
    {
        long total = 0;
        var folders = profile.Folders;

        foreach (var path in new[]
        {
            folders.Documents, folders.Desktop, folders.Downloads,
            folders.Pictures, folders.Music, folders.Videos, folders.Favorites
        })
        {
            if (path is not null)
                total += _fileSystem.GetDirectorySize(path);
        }

        total += profile.BrowserProfiles.Sum(b => b.EstimatedSizeBytes);
        total += profile.EmailData.Sum(e => e.EstimatedSizeBytes);

        return total;
    }
}
