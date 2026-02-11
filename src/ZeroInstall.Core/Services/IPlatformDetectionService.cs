using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Detects the operating system platform from a mounted drive's filesystem markers.
/// </summary>
public interface IPlatformDetectionService
{
    /// <summary>
    /// Detects the platform type by examining filesystem markers at the given root path.
    /// </summary>
    SourcePlatform DetectPlatform(string rootPath);

    /// <summary>
    /// Reads the OS version string from platform-specific files.
    /// </summary>
    string? GetOsVersion(string rootPath, SourcePlatform platform);
}
