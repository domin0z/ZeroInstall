using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Tier 1 migrator: installs applications via winget/chocolatey on the destination,
/// then overlays captured AppData and registry settings from the source.
/// </summary>
public class PackageMigratorService : IPackageMigrator
{
    private const string CaptureManifestFileName = "package-capture-manifest.json";
    private const string AppDataSubDir = "appdata";

    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemAccessor _fileSystem;
    private readonly AppDataCaptureHelper _appDataHelper;
    private readonly ILogger<PackageMigratorService> _logger;

    public PackageMigratorService(
        IProcessRunner processRunner,
        IFileSystemAccessor fileSystem,
        AppDataCaptureHelper appDataHelper,
        ILogger<PackageMigratorService> logger)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _appDataHelper = appDataHelper;
        _logger = logger;
    }

    /// <summary>
    /// Resolves which discovered applications can be installed via package managers.
    /// Prefers winget over chocolatey when both are available.
    /// </summary>
    public Task<IReadOnlyList<PackageInstallEntry>> ResolvePackagesAsync(
        IReadOnlyList<DiscoveredApplication> apps,
        CancellationToken ct = default)
    {
        var packages = new List<PackageInstallEntry>();

        foreach (var app in apps)
        {
            if (app.WingetPackageId is not null)
            {
                packages.Add(new PackageInstallEntry
                {
                    ApplicationName = app.Name,
                    PackageManager = "winget",
                    PackageId = app.WingetPackageId,
                    Version = app.Version
                });
            }
            else if (app.ChocolateyPackageId is not null)
            {
                packages.Add(new PackageInstallEntry
                {
                    ApplicationName = app.Name,
                    PackageManager = "chocolatey",
                    PackageId = app.ChocolateyPackageId,
                    Version = app.Version
                });
            }
        }

        _logger.LogInformation("Resolved {Count} package-installable apps out of {Total}",
            packages.Count, apps.Count);

        return Task.FromResult<IReadOnlyList<PackageInstallEntry>>(packages);
    }

    /// <summary>
    /// Captures application settings from the source machine.
    /// For Tier 1, this means capturing AppData folders and HKCU registry keys
    /// that will be overlaid after package install on the destination.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputPath);

        var packageItems = items
            .Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.Package && i.SourceData is DiscoveredApplication)
            .ToList();

        var captureManifest = new PackageCaptureManifest();

        for (int i = 0; i < packageItems.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item = packageItems[i];
            var app = (DiscoveredApplication)item.SourceData!;

            item.Status = MigrationItemStatus.InProgress;

            progress?.Report(new TransferProgress
            {
                CurrentItemName = app.Name,
                CurrentItemIndex = i + 1,
                TotalItems = packageItems.Count,
                OverallBytesTransferred = i,
                OverallTotalBytes = packageItems.Count
            });

            try
            {
                // Capture AppData and registry for this app
                var appOutputDir = Path.Combine(outputPath, AppDataSubDir, SanitizeDirectoryName(app.Name));
                var appManifest = await _appDataHelper.CaptureAsync(app, appOutputDir, ct);

                // Build the package install entry
                PackageInstallEntry? packageEntry = null;
                if (app.WingetPackageId is not null)
                {
                    packageEntry = new PackageInstallEntry
                    {
                        ApplicationName = app.Name,
                        PackageManager = "winget",
                        PackageId = app.WingetPackageId,
                        Version = app.Version
                    };
                }
                else if (app.ChocolateyPackageId is not null)
                {
                    packageEntry = new PackageInstallEntry
                    {
                        ApplicationName = app.Name,
                        PackageManager = "chocolatey",
                        PackageId = app.ChocolateyPackageId,
                        Version = app.Version
                    };
                }

                if (packageEntry is not null)
                {
                    captureManifest.Packages.Add(packageEntry);
                    captureManifest.AppDataDirs[app.Name] = SanitizeDirectoryName(app.Name);
                }

                item.Status = MigrationItemStatus.Completed;
                _logger.LogInformation("Captured settings for {App}", app.Name);
            }
            catch (Exception ex)
            {
                item.Status = MigrationItemStatus.Failed;
                item.StatusMessage = ex.Message;
                _logger.LogError(ex, "Failed to capture settings for {App}", app.Name);
            }
        }

        // Write the capture manifest
        var manifestPath = Path.Combine(outputPath, CaptureManifestFileName);
        var json = JsonSerializer.Serialize(captureManifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);

        _logger.LogInformation("Package capture complete: {Count} apps captured", captureManifest.Packages.Count);
    }

    /// <summary>
    /// Restores on the destination: installs packages, then overlays captured settings.
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(inputPath, CaptureManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Package capture manifest not found", manifestPath);

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var captureManifest = JsonSerializer.Deserialize<PackageCaptureManifest>(json)
            ?? throw new InvalidDataException("Failed to deserialize package capture manifest");

        // Step 1: Install all packages
        await InstallPackagesAsync(captureManifest.Packages, progress, ct);

        // Step 2: Overlay AppData and registry settings
        for (int i = 0; i < captureManifest.Packages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var package = captureManifest.Packages[i];

            if (captureManifest.AppDataDirs.TryGetValue(package.ApplicationName, out var dirName))
            {
                var captureDir = Path.Combine(inputPath, AppDataSubDir, dirName);
                if (Directory.Exists(captureDir))
                {
                    try
                    {
                        await _appDataHelper.RestoreAsync(captureDir, userMappings, ct);
                        _logger.LogInformation("Restored settings overlay for {App}", package.ApplicationName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore settings for {App}", package.ApplicationName);
                    }
                }
            }
        }

        _logger.LogInformation("Package restore complete");
    }

    /// <summary>
    /// Installs packages on the destination machine via winget or chocolatey.
    /// </summary>
    public async Task InstallPackagesAsync(
        IReadOnlyList<PackageInstallEntry> packages,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Check which package managers are available
        var wingetAvailable = await IsPackageManagerAvailableAsync("winget", ct);
        var chocoAvailable = await IsPackageManagerAvailableAsync("choco", ct);

        for (int i = 0; i < packages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var package = packages[i];

            progress?.Report(new TransferProgress
            {
                CurrentItemName = $"Installing {package.ApplicationName}",
                CurrentItemIndex = i + 1,
                TotalItems = packages.Count,
                OverallBytesTransferred = i,
                OverallTotalBytes = packages.Count
            });

            var result = package.PackageManager switch
            {
                "winget" when wingetAvailable => await InstallViaWingetAsync(package, ct),
                "chocolatey" when chocoAvailable => await InstallViaChocolateyAsync(package, ct),
                "winget" when !wingetAvailable && chocoAvailable && package.PackageId is not null =>
                    await TryFallbackInstallAsync(package, ct),
                _ => new PackageInstallResult
                {
                    Success = false,
                    Message = $"Package manager '{package.PackageManager}' is not available"
                }
            };

            if (result.Success)
            {
                _logger.LogInformation("Installed {App} via {Manager}",
                    package.ApplicationName, package.PackageManager);
            }
            else
            {
                _logger.LogWarning("Failed to install {App}: {Message}",
                    package.ApplicationName, result.Message);
            }
        }
    }

    internal async Task<PackageInstallResult> InstallViaWingetAsync(
        PackageInstallEntry package, CancellationToken ct)
    {
        var args = $"install --id \"{package.PackageId}\" --accept-package-agreements --accept-source-agreements --disable-interactivity";

        // Pin to specific version if provided
        if (!string.IsNullOrEmpty(package.Version))
            args += $" --version \"{package.Version}\"";

        _logger.LogDebug("Running: winget {Args}", args);

        var result = await _processRunner.RunAsync("winget", args, ct);

        return new PackageInstallResult
        {
            Success = result.Success,
            Message = result.Success ? "Installed successfully" : $"Exit code {result.ExitCode}: {result.StandardError}",
            StandardOutput = result.StandardOutput
        };
    }

    internal async Task<PackageInstallResult> InstallViaChocolateyAsync(
        PackageInstallEntry package, CancellationToken ct)
    {
        var args = $"install {package.PackageId} --yes --no-progress";

        if (!string.IsNullOrEmpty(package.Version))
            args += $" --version {package.Version}";

        _logger.LogDebug("Running: choco {Args}", args);

        var result = await _processRunner.RunAsync("choco", args, ct);

        return new PackageInstallResult
        {
            Success = result.Success,
            Message = result.Success ? "Installed successfully" : $"Exit code {result.ExitCode}: {result.StandardError}",
            StandardOutput = result.StandardOutput
        };
    }

    private async Task<PackageInstallResult> TryFallbackInstallAsync(
        PackageInstallEntry package, CancellationToken ct)
    {
        // If winget is preferred but unavailable, try choco with the same package name as a best-effort fallback
        _logger.LogInformation("Winget not available, attempting chocolatey fallback for {App}", package.ApplicationName);

        var fallback = new PackageInstallEntry
        {
            ApplicationName = package.ApplicationName,
            PackageManager = "chocolatey",
            PackageId = package.PackageId, // May not match choco ID — best effort
            Version = package.Version
        };

        return await InstallViaChocolateyAsync(fallback, ct);
    }

    internal async Task<bool> IsPackageManagerAvailableAsync(string command, CancellationToken ct)
    {
        try
        {
            var result = await _processRunner.RunAsync(command, "--version", ct);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    internal static string SanitizeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().TrimEnd('.');
    }
}

/// <summary>
/// Manifest describing a package-based capture (source side).
/// </summary>
public class PackageCaptureManifest
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public List<PackageInstallEntry> Packages { get; set; } = [];

    /// <summary>
    /// Maps application name to the subdirectory name under appdata/ where its settings were captured.
    /// </summary>
    public Dictionary<string, string> AppDataDirs { get; set; } = new();
}

/// <summary>
/// Result of attempting to install a single package.
/// </summary>
public class PackageInstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
}
