using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers user profiles from a mounted macOS volume by scanning /Users/.
/// </summary>
internal class MacOsUserProfileDiscoveryService
{
    private static readonly HashSet<string> SkippedUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shared", ".localized", "Guest"
    };

    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger _logger;

    public MacOsUserProfileDiscoveryService(IFileSystemAccessor fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<List<UserProfile>> DiscoverAsync(string rootPath, CancellationToken ct)
    {
        var root = rootPath.TrimEnd('\\', '/');
        var usersDir = Path.Combine(root, "Users");
        var profiles = new List<UserProfile>();

        if (!_fileSystem.DirectoryExists(usersDir))
            return Task.FromResult(profiles);

        var userDirs = _fileSystem.GetDirectories(usersDir);

        foreach (var userDir in userDirs)
        {
            ct.ThrowIfCancellationRequested();

            var username = Path.GetFileName(userDir);
            if (string.IsNullOrEmpty(username) || SkippedUsers.Contains(username))
                continue;

            // Skip hidden directories (starting with .)
            if (username.StartsWith('.'))
                continue;

            var profilePath = Path.Combine(usersDir, username);
            var profile = new UserProfile
            {
                Username = username,
                Sid = username, // Use folder name as identifier for macOS
                ProfilePath = profilePath,
                IsLocal = true,
                AccountType = UserAccountType.Local,
                Folders = DiscoverFolders(profilePath),
                BrowserProfiles = DiscoverBrowserProfiles(profilePath),
                EmailData = DiscoverEmailData(profilePath),
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(profilePath)
            };

            profiles.Add(profile);
            _logger.LogDebug("Discovered macOS user profile: {Username}", username);
        }

        return Task.FromResult(profiles);
    }

    private UserProfileFolders DiscoverFolders(string profilePath)
    {
        return new UserProfileFolders
        {
            Documents = ExistsOrNull(Path.Combine(profilePath, "Documents")),
            Desktop = ExistsOrNull(Path.Combine(profilePath, "Desktop")),
            Downloads = ExistsOrNull(Path.Combine(profilePath, "Downloads")),
            Pictures = ExistsOrNull(Path.Combine(profilePath, "Pictures")),
            Music = ExistsOrNull(Path.Combine(profilePath, "Music")),
            Videos = ExistsOrNull(Path.Combine(profilePath, "Movies")), // macOS uses "Movies"
            AppDataRoaming = ExistsOrNull(Path.Combine(profilePath, "Library", "Application Support")),
            AppDataLocal = ExistsOrNull(Path.Combine(profilePath, "Library", "Caches"))
        };
    }

    private List<BrowserProfile> DiscoverBrowserProfiles(string profilePath)
    {
        var browsers = new List<BrowserProfile>();

        // Chrome
        var chromePath = Path.Combine(profilePath, "Library", "Application Support", "Google", "Chrome");
        if (_fileSystem.DirectoryExists(chromePath))
        {
            browsers.Add(new BrowserProfile
            {
                BrowserName = "Chrome",
                ProfilePath = chromePath,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(chromePath)
            });
        }

        // Firefox
        var firefoxPath = Path.Combine(profilePath, "Library", "Application Support", "Firefox", "Profiles");
        if (_fileSystem.DirectoryExists(firefoxPath))
        {
            foreach (var ffProfile in _fileSystem.GetDirectories(firefoxPath))
            {
                browsers.Add(new BrowserProfile
                {
                    BrowserName = "Firefox",
                    ProfilePath = ffProfile,
                    ProfileName = Path.GetFileName(ffProfile),
                    EstimatedSizeBytes = _fileSystem.GetDirectorySize(ffProfile)
                });
            }
        }

        // Safari
        var safariPath = Path.Combine(profilePath, "Library", "Safari");
        if (_fileSystem.DirectoryExists(safariPath))
        {
            browsers.Add(new BrowserProfile
            {
                BrowserName = "Safari",
                ProfilePath = safariPath,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(safariPath)
            });
        }

        // Edge
        var edgePath = Path.Combine(profilePath, "Library", "Application Support", "Microsoft Edge");
        if (_fileSystem.DirectoryExists(edgePath))
        {
            browsers.Add(new BrowserProfile
            {
                BrowserName = "Edge",
                ProfilePath = edgePath,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(edgePath)
            });
        }

        return browsers;
    }

    private List<EmailClientData> DiscoverEmailData(string profilePath)
    {
        var email = new List<EmailClientData>();

        // Outlook
        var outlookPath = Path.Combine(profilePath, "Library", "Group Containers",
            "UBF8T346G9.Office", "Outlook", "Outlook 15 Profiles");
        if (_fileSystem.DirectoryExists(outlookPath))
        {
            email.Add(new EmailClientData
            {
                ClientName = "Outlook",
                DataPaths = [outlookPath],
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(outlookPath)
            });
        }

        // Thunderbird
        var thunderbirdPath = Path.Combine(profilePath, "Library", "Thunderbird", "Profiles");
        if (_fileSystem.DirectoryExists(thunderbirdPath))
        {
            email.Add(new EmailClientData
            {
                ClientName = "Thunderbird",
                DataPaths = [thunderbirdPath],
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(thunderbirdPath)
            });
        }

        // Apple Mail
        var mailPath = Path.Combine(profilePath, "Library", "Mail");
        if (_fileSystem.DirectoryExists(mailPath))
        {
            email.Add(new EmailClientData
            {
                ClientName = "Apple Mail",
                DataPaths = [mailPath],
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(mailPath)
            });
        }

        return email;
    }

    private string? ExistsOrNull(string path) =>
        _fileSystem.DirectoryExists(path) ? path : null;
}
