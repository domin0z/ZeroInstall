using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.App.Models;

namespace ZeroInstall.App.Services;

/// <summary>
/// JSON file-backed implementation of <see cref="IAppSettings"/>.
/// Settings file lives at {configPath}/settings.json.
/// </summary>
internal sealed class JsonAppSettings : IAppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<JsonAppSettings> _logger;

    public AppSettings Current { get; private set; } = new();

    public JsonAppSettings(string configPath, ILogger<JsonAppSettings> logger)
    {
        _filePath = Path.Combine(configPath, "settings.json");
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("Settings file not found at {Path}, using defaults", _filePath);
            Current = new AppSettings();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            _logger.LogInformation("Settings loaded from {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from {Path}, using defaults", _filePath);
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        Current = settings;
        _logger.LogInformation("Settings saved to {Path}", _filePath);
    }
}
