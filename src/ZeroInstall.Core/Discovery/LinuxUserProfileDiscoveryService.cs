using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers user profiles from a mounted Linux volume by parsing /etc/passwd and scanning /home/.
/// </summary>
internal class LinuxUserProfileDiscoveryService
{
    private static readonly HashSet<string> NoLoginShells = new(StringComparer.OrdinalIgnoreCase)
    {
        "/usr/sbin/nologin",
        "/bin/false",
        "/sbin/nologin"
    };

    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger _logger;

    public LinuxUserProfileDiscoveryService(IFileSystemAccessor fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<List<UserProfile>> DiscoverAsync(string rootPath, CancellationToken ct)
    {
        var root = rootPath.TrimEnd('\\', '/');
        var profiles = new List<UserProfile>();

        var passwdPath = Path.Combine(root, "etc", "passwd");
        if (!_fileSystem.FileExists(passwdPath))
            return Task.FromResult(profiles);

        var lines = _fileSystem.ReadAllLines(passwdPath);
        var entries = ParseEtcPasswd(lines);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            // Rebase home directory to the mounted root path
            var rebasedHome = RebaseHomePath(root, entry.HomeDir);
            if (!_fileSystem.DirectoryExists(rebasedHome))
                continue;

            var profile = new UserProfile
            {
                Username = entry.Username,
                Sid = entry.Uid.ToString(),
                ProfilePath = rebasedHome,
                IsLocal = true,
                AccountType = UserAccountType.Local,
                Folders = DiscoverFolders(rebasedHome),
                BrowserProfiles = DiscoverBrowserProfiles(rebasedHome),
                EmailData = DiscoverEmailData(rebasedHome),
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(rebasedHome)
            };

            profiles.Add(profile);
            _logger.LogDebug("Discovered Linux user profile: {Username} (UID {Uid})", entry.Username, entry.Uid);
        }

        return Task.FromResult(profiles);
    }

    /// <summary>
    /// Parses /etc/passwd lines into user entries, filtering for real user accounts (UID >= 1000, valid shell).
    /// </summary>
    internal static List<PasswdEntry> ParseEtcPasswd(string[] lines)
    {
        var entries = new List<PasswdEntry>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split(':');
            if (parts.Length < 7)
                continue;

            if (!int.TryParse(parts[2], out var uid))
                continue;

            // Filter system users (UID < 1000) except root (we skip root too)
            if (uid < 1000)
                continue;

            var shell = parts[6];
            if (NoLoginShells.Contains(shell))
                continue;

            entries.Add(new PasswdEntry
            {
                Username = parts[0],
                Uid = uid,
                HomeDir = parts[5],
                Shell = shell
            });
        }

        return entries;
    }

    private UserProfileFolders DiscoverFolders(string homePath)
    {
        return new UserProfileFolders
        {
            Documents = ExistsOrNull(Path.Combine(homePath, "Documents")),
            Desktop = ExistsOrNull(Path.Combine(homePath, "Desktop")),
            Downloads = ExistsOrNull(Path.Combine(homePath, "Downloads")),
            Pictures = ExistsOrNull(Path.Combine(homePath, "Pictures")),
            Music = ExistsOrNull(Path.Combine(homePath, "Music")),
            Videos = ExistsOrNull(Path.Combine(homePath, "Videos")),
            AppDataRoaming = ExistsOrNull(Path.Combine(homePath, ".config")),
            AppDataLocal = ExistsOrNull(Path.Combine(homePath, ".local", "share"))
        };
    }

    private List<BrowserProfile> DiscoverBrowserProfiles(string homePath)
    {
        var browsers = new List<BrowserProfile>();

        // Chrome
        var chromePath = Path.Combine(homePath, ".config", "google-chrome");
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
        var firefoxPath = Path.Combine(homePath, ".mozilla", "firefox");
        if (_fileSystem.DirectoryExists(firefoxPath))
        {
            foreach (var ffProfile in _fileSystem.GetDirectories(firefoxPath))
            {
                var profileName = Path.GetFileName(ffProfile);
                // Skip non-profile directories
                if (profileName.Contains('.'))
                {
                    browsers.Add(new BrowserProfile
                    {
                        BrowserName = "Firefox",
                        ProfilePath = ffProfile,
                        ProfileName = profileName,
                        EstimatedSizeBytes = _fileSystem.GetDirectorySize(ffProfile)
                    });
                }
            }
        }

        // Chromium
        var chromiumPath = Path.Combine(homePath, ".config", "chromium");
        if (_fileSystem.DirectoryExists(chromiumPath))
        {
            browsers.Add(new BrowserProfile
            {
                BrowserName = "Chromium",
                ProfilePath = chromiumPath,
                ProfileName = "Default",
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(chromiumPath)
            });
        }

        return browsers;
    }

    private List<EmailClientData> DiscoverEmailData(string homePath)
    {
        var email = new List<EmailClientData>();

        // Thunderbird
        var thunderbirdPath = Path.Combine(homePath, ".thunderbird");
        if (_fileSystem.DirectoryExists(thunderbirdPath))
        {
            email.Add(new EmailClientData
            {
                ClientName = "Thunderbird",
                DataPaths = [thunderbirdPath],
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(thunderbirdPath)
            });
        }

        // Evolution
        var evolutionPath = Path.Combine(homePath, ".local", "share", "evolution", "mail");
        if (_fileSystem.DirectoryExists(evolutionPath))
        {
            email.Add(new EmailClientData
            {
                ClientName = "Evolution",
                DataPaths = [evolutionPath],
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(evolutionPath)
            });
        }

        return email;
    }

    private string? ExistsOrNull(string path) =>
        _fileSystem.DirectoryExists(path) ? path : null;

    /// <summary>
    /// Rebases a Linux home path (e.g., /home/john) to the mounted root (e.g., E:\home\john).
    /// </summary>
    private static string RebaseHomePath(string root, string homeDir)
    {
        // Convert forward-slash Linux path to Windows path under the mount root
        var relativeParts = homeDir.TrimStart('/').Split('/');
        return Path.Combine([root, .. relativeParts]);
    }

    internal record PasswdEntry
    {
        public string Username { get; init; } = string.Empty;
        public int Uid { get; init; }
        public string HomeDir { get; init; } = string.Empty;
        public string Shell { get; init; } = string.Empty;
    }
}
