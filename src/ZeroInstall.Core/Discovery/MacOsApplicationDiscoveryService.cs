using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers applications from a mounted macOS volume by scanning /Applications/ and Homebrew.
/// </summary>
internal class MacOsApplicationDiscoveryService
{
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger _logger;

    public MacOsApplicationDiscoveryService(IFileSystemAccessor fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<List<DiscoveredApplication>> DiscoverAsync(string rootPath, CancellationToken ct)
    {
        var root = rootPath.TrimEnd('\\', '/');
        var apps = new List<DiscoveredApplication>();

        // Scan /Applications/ for .app bundles
        DiscoverAppBundles(root, apps, ct);

        // Scan Homebrew Cellar for CLI installs (Intel)
        DiscoverHomebrewCellar(Path.Combine(root, "usr", "local", "Cellar"), apps, ct);

        // Scan Homebrew Cellar for CLI installs (Apple Silicon)
        DiscoverHomebrewCellar(Path.Combine(root, "opt", "homebrew", "Cellar"), apps, ct);

        // Scan Homebrew Caskroom (Intel)
        DiscoverHomebrewCaskroom(Path.Combine(root, "usr", "local", "Caskroom"), apps, ct);

        // Scan Homebrew Caskroom (Apple Silicon)
        DiscoverHomebrewCaskroom(Path.Combine(root, "opt", "homebrew", "Caskroom"), apps, ct);

        return Task.FromResult(apps);
    }

    private void DiscoverAppBundles(string root, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        var appsDir = Path.Combine(root, "Applications");
        if (!_fileSystem.DirectoryExists(appsDir))
            return;

        var appBundles = _fileSystem.GetDirectories(appsDir);
        foreach (var bundle in appBundles)
        {
            ct.ThrowIfCancellationRequested();

            var bundleName = Path.GetFileName(bundle);
            if (!bundleName.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                continue;

            var infoPlistPath = Path.Combine(bundle, "Contents", "Info.plist");
            var (name, version, bundleId) = ParseInfoPlistFile(infoPlistPath);

            if (string.IsNullOrEmpty(name))
                name = bundleName.Replace(".app", "", StringComparison.OrdinalIgnoreCase);

            var publisher = ExtractPublisher(bundleId);

            apps.Add(new DiscoveredApplication
            {
                Name = name,
                Version = version ?? string.Empty,
                Publisher = publisher ?? string.Empty,
                InstallLocation = bundle,
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(bundle)
            });

            _logger.LogDebug("Discovered macOS app: {Name} {Version}", name, version);
        }
    }

    private void DiscoverHomebrewCellar(string cellarPath, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        if (!_fileSystem.DirectoryExists(cellarPath))
            return;

        var formulaDirs = _fileSystem.GetDirectories(cellarPath);
        foreach (var formulaDir in formulaDirs)
        {
            ct.ThrowIfCancellationRequested();

            var formulaName = Path.GetFileName(formulaDir);
            var versionDirs = _fileSystem.GetDirectories(formulaDir);
            var latestVersion = versionDirs.Length > 0
                ? Path.GetFileName(versionDirs[^1])
                : string.Empty;

            // Skip if already discovered from /Applications/
            if (apps.Any(a => a.Name.Equals(formulaName, StringComparison.OrdinalIgnoreCase)))
                continue;

            apps.Add(new DiscoveredApplication
            {
                Name = formulaName,
                Version = latestVersion ?? string.Empty,
                Publisher = "Homebrew",
                InstallLocation = formulaDir,
                EstimatedSizeBytes = _fileSystem.GetDirectorySize(formulaDir)
            });

            _logger.LogDebug("Discovered Homebrew formula: {Name} {Version}", formulaName, latestVersion);
        }
    }

    private void DiscoverHomebrewCaskroom(string caskroomPath, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        if (!_fileSystem.DirectoryExists(caskroomPath))
            return;

        var caskDirs = _fileSystem.GetDirectories(caskroomPath);
        foreach (var caskDir in caskDirs)
        {
            ct.ThrowIfCancellationRequested();

            var caskName = Path.GetFileName(caskDir);
            var versionDirs = _fileSystem.GetDirectories(caskDir);
            var latestVersion = versionDirs.Length > 0
                ? Path.GetFileName(versionDirs[^1])
                : string.Empty;

            // Try to find existing app and tag it
            var existing = apps.FirstOrDefault(a =>
                a.Name.Equals(caskName, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Replace(" ", "-", StringComparison.OrdinalIgnoreCase)
                    .Equals(caskName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.BrewCaskId = caskName;
            }
            else
            {
                apps.Add(new DiscoveredApplication
                {
                    Name = caskName,
                    Version = latestVersion ?? string.Empty,
                    Publisher = "Homebrew Cask",
                    InstallLocation = caskDir,
                    BrewCaskId = caskName,
                    EstimatedSizeBytes = _fileSystem.GetDirectorySize(caskDir)
                });
            }

            _logger.LogDebug("Discovered Homebrew cask: {CaskName}", caskName);
        }
    }

    private (string? name, string? version, string? bundleId) ParseInfoPlistFile(string plistPath)
    {
        if (!_fileSystem.FileExists(plistPath))
            return (null, null, null);

        try
        {
            var content = _fileSystem.ReadAllText(plistPath);
            return ParseInfoPlist(content);
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// <summary>
    /// Parses CFBundleName, CFBundleShortVersionString, and CFBundleIdentifier from an Info.plist.
    /// </summary>
    internal static (string? name, string? version, string? bundleId) ParseInfoPlist(string content)
    {
        try
        {
            var doc = XDocument.Parse(content);
            var dict = doc.Root?.Element("dict");
            if (dict is null) return (null, null, null);

            var elements = dict.Elements().ToList();
            string? name = null, version = null, bundleId = null;

            for (int i = 0; i < elements.Count - 1; i++)
            {
                if (elements[i].Name != "key") continue;

                var keyValue = elements[i].Value;
                var nextElement = elements[i + 1];

                switch (keyValue)
                {
                    case "CFBundleName":
                        name = nextElement.Value;
                        break;
                    case "CFBundleShortVersionString":
                        version = nextElement.Value;
                        break;
                    case "CFBundleIdentifier":
                        bundleId = nextElement.Value;
                        break;
                }
            }

            return (name, version, bundleId);
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// <summary>
    /// Extracts a publisher name from a bundle identifier (e.g., "com.google.Chrome" -> "Google").
    /// </summary>
    internal static string? ExtractPublisher(string? bundleId)
    {
        if (string.IsNullOrEmpty(bundleId)) return null;

        var parts = bundleId.Split('.');
        if (parts.Length < 2) return null;

        // Capitalize first letter of the second component
        var publisher = parts[1];
        if (publisher.Length == 0) return null;

        return char.ToUpper(publisher[0]) + publisher[1..];
    }
}
