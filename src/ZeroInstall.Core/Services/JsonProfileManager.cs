using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// File-based JSON implementation of <see cref="IProfileManager"/>.
/// Reads/writes MigrationProfile JSON files from local and optional NAS paths.
/// </summary>
public partial class JsonProfileManager : IProfileManager
{
    private readonly string _localProfilesPath;
    private readonly string? _nasProfilesPath;
    private readonly ILogger<JsonProfileManager> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonProfileManager(string localProfilesPath, string? nasProfilesPath, ILogger<JsonProfileManager> logger)
    {
        ArgumentNullException.ThrowIfNull(localProfilesPath);
        _localProfilesPath = localProfilesPath;
        _nasProfilesPath = nasProfilesPath;
        _logger = logger;

        Directory.CreateDirectory(_localProfilesPath);
    }

    public async Task<IReadOnlyList<MigrationProfile>> ListLocalProfilesAsync(CancellationToken ct = default)
    {
        return await ListProfilesFromPathAsync(_localProfilesPath, ct);
    }

    public async Task<IReadOnlyList<MigrationProfile>> ListNasProfilesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_nasProfilesPath))
        {
            _logger.LogDebug("NAS path not configured; returning empty list");
            return Array.Empty<MigrationProfile>();
        }

        if (!Directory.Exists(_nasProfilesPath))
        {
            _logger.LogWarning("NAS profiles path does not exist: {Path}", _nasProfilesPath);
            return Array.Empty<MigrationProfile>();
        }

        return await ListProfilesFromPathAsync(_nasProfilesPath, ct);
    }

    public async Task<MigrationProfile?> LoadLocalProfileAsync(string name, CancellationToken ct = default)
    {
        return await LoadProfileFromPathAsync(_localProfilesPath, name, ct);
    }

    public async Task<MigrationProfile?> LoadNasProfileAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_nasProfilesPath) || !Directory.Exists(_nasProfilesPath))
            return null;

        return await LoadProfileFromPathAsync(_nasProfilesPath, name, ct);
    }

    public async Task SaveLocalProfileAsync(MigrationProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("Profile name cannot be empty.", nameof(profile));

        profile.ModifiedUtc = DateTime.UtcNow;

        var fileName = SanitizeFileName(profile.Name) + ".json";
        var filePath = Path.Combine(_localProfilesPath, fileName);

        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);

        _logger.LogInformation("Saved profile '{Name}' to {Path}", profile.Name, filePath);
    }

    public Task DeleteLocalProfileAsync(string name, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var fileName = SanitizeFileName(name) + ".json";
        var filePath = Path.Combine(_localProfilesPath, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted profile '{Name}' from {Path}", name, filePath);
        }
        else
        {
            _logger.LogWarning("Profile '{Name}' not found at {Path}", name, filePath);
        }

        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<MigrationProfile>> ListProfilesFromPathAsync(string path, CancellationToken ct)
    {
        var profiles = new List<MigrationProfile>();

        if (!Directory.Exists(path))
            return profiles.AsReadOnly();

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var profile = JsonSerializer.Deserialize<MigrationProfile>(json, JsonOptions);
                if (profile is not null)
                    profiles.Add(profile);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize profile file {File}", file);
            }
        }

        return profiles
            .OrderBy(p => p.Name)
            .ToList()
            .AsReadOnly();
    }

    private async Task<MigrationProfile?> LoadProfileFromPathAsync(string basePath, string name, CancellationToken ct)
    {
        var fileName = SanitizeFileName(name) + ".json";
        var filePath = Path.Combine(basePath, fileName);

        if (!File.Exists(filePath))
        {
            // Also try matching by profile name inside files
            return await FindProfileByNameAsync(basePath, name, ct);
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<MigrationProfile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize profile {File}", filePath);
            return null;
        }
    }

    private async Task<MigrationProfile?> FindProfileByNameAsync(string basePath, string name, CancellationToken ct)
    {
        if (!Directory.Exists(basePath))
            return null;

        foreach (var file in Directory.GetFiles(basePath, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var profile = JsonSerializer.Deserialize<MigrationProfile>(json, JsonOptions);
                if (profile is not null &&
                    string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            catch (JsonException)
            {
                // Skip invalid files
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a profile name to a safe filename by removing invalid characters.
    /// </summary>
    internal static string SanitizeFileName(string name)
    {
        // Replace spaces and common separators with hyphens
        var sanitized = InvalidFileNameCharsRegex().Replace(name.Trim(), "-");
        // Collapse multiple hyphens
        sanitized = MultipleHyphensRegex().Replace(sanitized, "-");
        // Trim leading/trailing hyphens
        return sanitized.Trim('-');
    }

    [GeneratedRegex(@"[^\w\-\.]")]
    private static partial Regex InvalidFileNameCharsRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
