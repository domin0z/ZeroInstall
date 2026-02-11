using System.Xml.Linq;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Detects macOS, Linux, or Windows by examining filesystem markers on a mounted drive.
/// </summary>
internal class PlatformDetectionService : IPlatformDetectionService
{
    private readonly IFileSystemAccessor _fileSystem;

    public PlatformDetectionService(IFileSystemAccessor fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public SourcePlatform DetectPlatform(string rootPath)
    {
        var root = rootPath.TrimEnd('\\', '/');

        // macOS: /System/Library/CoreServices/ AND /Users/ AND /Applications/
        if (_fileSystem.DirectoryExists(Path.Combine(root, "System", "Library", "CoreServices"))
            && _fileSystem.DirectoryExists(Path.Combine(root, "Users"))
            && _fileSystem.DirectoryExists(Path.Combine(root, "Applications")))
        {
            return SourcePlatform.MacOs;
        }

        // Linux: /etc/os-release OR (/etc/passwd AND /home/)
        if (_fileSystem.FileExists(Path.Combine(root, "etc", "os-release"))
            || (_fileSystem.FileExists(Path.Combine(root, "etc", "passwd"))
                && _fileSystem.DirectoryExists(Path.Combine(root, "home"))))
        {
            return SourcePlatform.Linux;
        }

        // Windows: Windows\System32\ AND Users\
        if (_fileSystem.DirectoryExists(Path.Combine(root, "Windows", "System32"))
            && _fileSystem.DirectoryExists(Path.Combine(root, "Users")))
        {
            return SourcePlatform.Windows;
        }

        return SourcePlatform.Unknown;
    }

    public string? GetOsVersion(string rootPath, SourcePlatform platform)
    {
        var root = rootPath.TrimEnd('\\', '/');

        return platform switch
        {
            SourcePlatform.MacOs => GetMacOsVersion(root),
            SourcePlatform.Linux => GetLinuxOsVersion(root),
            _ => null
        };
    }

    private string? GetMacOsVersion(string root)
    {
        var plistPath = Path.Combine(root, "System", "Library", "CoreServices", "SystemVersion.plist");
        if (!_fileSystem.FileExists(plistPath))
            return null;

        try
        {
            var content = _fileSystem.ReadAllText(plistPath);
            return ParseMacOsVersion(content);
        }
        catch
        {
            return null;
        }
    }

    private string? GetLinuxOsVersion(string root)
    {
        var osReleasePath = Path.Combine(root, "etc", "os-release");
        if (!_fileSystem.FileExists(osReleasePath))
            return null;

        try
        {
            var content = _fileSystem.ReadAllText(osReleasePath);
            return ParseLinuxOsRelease(content);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the ProductVersion from a macOS SystemVersion.plist XML content.
    /// </summary>
    internal static string? ParseMacOsVersion(string plistContent)
    {
        try
        {
            var doc = XDocument.Parse(plistContent);
            var dict = doc.Root?.Element("dict");
            if (dict is null) return null;

            var keys = dict.Elements("key").ToList();
            var values = dict.Elements("string").ToList();

            for (int i = 0; i < keys.Count && i < values.Count; i++)
            {
                if (keys[i].Value == "ProductVersion")
                    return values[i].Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the PRETTY_NAME from Linux /etc/os-release content.
    /// </summary>
    internal static string? ParseLinuxOsRelease(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
            {
                var value = trimmed["PRETTY_NAME=".Length..];
                return value.Trim('"');
            }
        }

        return null;
    }
}
