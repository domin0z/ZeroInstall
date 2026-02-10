using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Migration;

namespace ZeroInstall.WinPE.Services;

/// <summary>
/// Result of scanning a directory for disk images.
/// </summary>
public class ImageSearchResult
{
    public string ImagePath { get; set; } = string.Empty;
    public DiskImageMetadata? Metadata { get; set; }
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Scans directories for disk image files (.img, .raw, .vhdx) with optional metadata.
/// </summary>
public class ImageBrowserService
{
    private static readonly string[] ImageExtensions = [".img", ".raw", ".vhdx"];

    private readonly ILogger<ImageBrowserService> _logger;

    public ImageBrowserService(ILogger<ImageBrowserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Finds disk image files in the specified directory (recursively).
    /// </summary>
    public async Task<List<ImageSearchResult>> FindImagesAsync(string searchPath, CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for disk images in {Path}", searchPath);

        var results = new List<ImageSearchResult>();

        if (!Directory.Exists(searchPath))
        {
            _logger.LogWarning("Search path does not exist: {Path}", searchPath);
            return results;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied scanning {Path}", searchPath);
            return results;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!ImageExtensions.Contains(ext))
                continue;

            // Skip chunk files (.partNNNN)
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            if (nameWithoutExt.Contains(".part"))
                continue;

            var result = new ImageSearchResult
            {
                ImagePath = file,
                FileSizeBytes = new FileInfo(file).Length
            };

            // Try to load metadata sidecar
            try
            {
                result.Metadata = await DiskImageMetadata.LoadAsync(file, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load metadata for {Path}", file);
            }

            results.Add(result);
        }

        _logger.LogInformation("Found {Count} disk image(s) in {Path}", results.Count, searchPath);
        return results;
    }
}
