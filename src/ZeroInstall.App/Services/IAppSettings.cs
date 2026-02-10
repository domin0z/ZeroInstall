using ZeroInstall.App.Models;

namespace ZeroInstall.App.Services;

/// <summary>
/// Loads and saves application settings from JSON.
/// </summary>
public interface IAppSettings
{
    /// <summary>
    /// The currently loaded settings.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Loads settings from disk (or creates defaults if missing).
    /// </summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
