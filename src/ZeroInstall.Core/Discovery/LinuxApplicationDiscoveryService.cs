using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Discovers applications from a mounted Linux volume by scanning .desktop files, dpkg, snap, and flatpak.
/// </summary>
internal class LinuxApplicationDiscoveryService
{
    private readonly IFileSystemAccessor _fileSystem;
    private readonly ILogger _logger;

    public LinuxApplicationDiscoveryService(IFileSystemAccessor fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<List<DiscoveredApplication>> DiscoverAsync(string rootPath, CancellationToken ct)
    {
        var root = rootPath.TrimEnd('\\', '/');
        var apps = new List<DiscoveredApplication>();

        // Scan .desktop files
        DiscoverDesktopFiles(root, apps, ct);

        // Parse dpkg/status for installed deb packages
        var dpkgPackages = ParseDpkgStatusFile(root);

        // Match .desktop apps to dpkg packages
        foreach (var app in apps)
        {
            MatchDesktopToPackage(app, dpkgPackages);
        }

        // Add dpkg packages that weren't discovered via .desktop files
        AddUnmatchedDpkgPackages(apps, dpkgPackages);

        // Discover snap installs
        DiscoverSnapInstalls(root, apps, ct);

        // Discover flatpak installs
        DiscoverFlatpakInstalls(root, apps, ct);

        return Task.FromResult(apps);
    }

    private void DiscoverDesktopFiles(string root, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        var desktopDir = Path.Combine(root, "usr", "share", "applications");
        if (!_fileSystem.DirectoryExists(desktopDir))
            return;

        var desktopFiles = _fileSystem.GetFiles(desktopDir, "*.desktop");
        foreach (var file in desktopFiles)
        {
            ct.ThrowIfCancellationRequested();

            var content = _fileSystem.ReadAllText(file);
            if (string.IsNullOrEmpty(content))
                continue;

            var entry = ParseDesktopFile(content);
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            apps.Add(new DiscoveredApplication
            {
                Name = entry.Name,
                Version = entry.Version ?? string.Empty,
                Publisher = entry.Comment ?? string.Empty,
                InstallLocation = entry.Exec
            });

            _logger.LogDebug("Discovered Linux desktop app: {Name}", entry.Name);
        }
    }

    private List<DpkgEntry> ParseDpkgStatusFile(string root)
    {
        var statusPath = Path.Combine(root, "var", "lib", "dpkg", "status");
        if (!_fileSystem.FileExists(statusPath))
            return [];

        var content = _fileSystem.ReadAllText(statusPath);
        return ParseDpkgStatus(content);
    }

    private static void AddUnmatchedDpkgPackages(List<DiscoveredApplication> apps, List<DpkgEntry> packages)
    {
        // Only add packages that have a meaningful description and aren't already matched
        var matchedPackageNames = apps
            .Where(a => a.AptPackageName is not null)
            .Select(a => a.AptPackageName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Don't add all dpkg packages (there are thousands); only add ones that weren't matched
        // but appear user-facing (have a description and aren't lib/dev packages)
        foreach (var pkg in packages)
        {
            if (matchedPackageNames.Contains(pkg.PackageName))
                continue;

            // Skip library and development packages
            if (pkg.PackageName.StartsWith("lib", StringComparison.Ordinal) ||
                pkg.PackageName.EndsWith("-dev", StringComparison.Ordinal) ||
                pkg.PackageName.EndsWith("-common", StringComparison.Ordinal) ||
                pkg.PackageName.EndsWith("-data", StringComparison.Ordinal))
                continue;
        }
    }

    private void DiscoverSnapInstalls(string root, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        var snapDir = Path.Combine(root, "snap");
        if (!_fileSystem.DirectoryExists(snapDir))
            return;

        var snapDirs = _fileSystem.GetDirectories(snapDir);
        foreach (var dir in snapDirs)
        {
            ct.ThrowIfCancellationRequested();

            var snapName = Path.GetFileName(dir);
            // Skip internal snap directories
            if (snapName is "bin" or "snapd" || string.IsNullOrEmpty(snapName))
                continue;

            // Check if already discovered and tag it
            var existing = apps.FirstOrDefault(a =>
                a.Name.Equals(snapName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.SnapPackageName = snapName;
            }
            else
            {
                apps.Add(new DiscoveredApplication
                {
                    Name = snapName,
                    Publisher = "Snap",
                    SnapPackageName = snapName,
                    EstimatedSizeBytes = _fileSystem.GetDirectorySize(dir)
                });
            }

            _logger.LogDebug("Discovered Snap package: {Name}", snapName);
        }
    }

    private void DiscoverFlatpakInstalls(string root, List<DiscoveredApplication> apps, CancellationToken ct)
    {
        var flatpakDir = Path.Combine(root, "var", "lib", "flatpak", "app");
        if (!_fileSystem.DirectoryExists(flatpakDir))
            return;

        var flatpakDirs = _fileSystem.GetDirectories(flatpakDir);
        foreach (var dir in flatpakDirs)
        {
            ct.ThrowIfCancellationRequested();

            var appId = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(appId))
                continue;

            // Extract app name from flatpak ID (e.g., "org.mozilla.firefox" -> "firefox")
            var parts = appId.Split('.');
            var shortName = parts.Length > 0 ? parts[^1] : appId;

            // Check if already discovered and tag it
            var existing = apps.FirstOrDefault(a =>
                a.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.FlatpakAppId = appId;
            }
            else
            {
                apps.Add(new DiscoveredApplication
                {
                    Name = shortName,
                    Publisher = "Flatpak",
                    FlatpakAppId = appId,
                    EstimatedSizeBytes = _fileSystem.GetDirectorySize(dir)
                });
            }

            _logger.LogDebug("Discovered Flatpak app: {AppId}", appId);
        }
    }

    /// <summary>
    /// Parses a .desktop file to extract Name, Version, Exec, and Comment fields.
    /// </summary>
    internal static DesktopEntry ParseDesktopFile(string content)
    {
        var entry = new DesktopEntry();
        var inDesktopEntry = false;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed == "[Desktop Entry]")
            {
                inDesktopEntry = true;
                continue;
            }

            // Stop when we hit the next section
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && inDesktopEntry)
                break;

            if (!inDesktopEntry || !trimmed.Contains('='))
                continue;

            var eqIdx = trimmed.IndexOf('=');
            var key = trimmed[..eqIdx].Trim();
            var value = trimmed[(eqIdx + 1)..].Trim();

            switch (key)
            {
                case "Name":
                    entry.Name = value;
                    break;
                case "Version":
                    entry.Version = value;
                    break;
                case "Exec":
                    entry.Exec = value;
                    break;
                case "Comment":
                    entry.Comment = value;
                    break;
            }
        }

        return entry;
    }

    /// <summary>
    /// Parses the dpkg/status file content into a list of installed packages.
    /// </summary>
    internal static List<DpkgEntry> ParseDpkgStatus(string content)
    {
        var packages = new List<DpkgEntry>();
        var blocks = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var entry = new DpkgEntry();
            var isInstalled = false;

            foreach (var line in block.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(' ') || trimmed.StartsWith('\t'))
                    continue; // Continuation line

                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx < 0) continue;

                var key = trimmed[..colonIdx].Trim();
                var value = trimmed[(colonIdx + 1)..].Trim();

                switch (key)
                {
                    case "Package":
                        entry.PackageName = value;
                        break;
                    case "Version":
                        entry.Version = value;
                        break;
                    case "Description":
                        entry.Description = value;
                        break;
                    case "Status":
                        isInstalled = value.Contains("installed");
                        break;
                }
            }

            if (isInstalled && !string.IsNullOrEmpty(entry.PackageName))
                packages.Add(entry);
        }

        return packages;
    }

    /// <summary>
    /// Attempts to match a discovered desktop app to a dpkg package by exec path.
    /// </summary>
    internal static void MatchDesktopToPackage(DiscoveredApplication app, List<DpkgEntry> packages)
    {
        if (string.IsNullOrEmpty(app.InstallLocation))
            return;

        // Extract the binary name from the Exec line
        var exec = app.InstallLocation.Split(' ')[0];
        var binaryName = exec.Contains('/') ? exec.Split('/')[^1] : exec;

        var match = packages.FirstOrDefault(p =>
            p.PackageName.Equals(binaryName, StringComparison.OrdinalIgnoreCase) ||
            p.PackageName.Equals(app.Name, StringComparison.OrdinalIgnoreCase) ||
            p.PackageName.Replace("-", "", StringComparison.Ordinal)
                .Equals(app.Name.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            app.AptPackageName = match.PackageName;
            if (string.IsNullOrEmpty(app.Version) && !string.IsNullOrEmpty(match.Version))
                app.Version = match.Version;
        }
    }

    internal record DesktopEntry
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Exec { get; set; }
        public string? Comment { get; set; }
    }

    internal record DpkgEntry
    {
        public string PackageName { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? Description { get; set; }
    }
}
