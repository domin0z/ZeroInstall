using Microsoft.Win32;

namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Abstraction over Windows Registry access for testability.
/// </summary>
public interface IRegistryAccessor
{
    /// <summary>
    /// Gets the subkey names under the given registry path.
    /// </summary>
    string[] GetSubKeyNames(RegistryHive hive, RegistryView view, string subKeyPath);

    /// <summary>
    /// Gets a string value from a registry key.
    /// </summary>
    string? GetStringValue(RegistryHive hive, RegistryView view, string subKeyPath, string valueName);

    /// <summary>
    /// Gets a DWORD value from a registry key.
    /// </summary>
    int? GetDwordValue(RegistryHive hive, RegistryView view, string subKeyPath, string valueName);

    /// <summary>
    /// Gets all value names under a registry key.
    /// </summary>
    string[] GetValueNames(RegistryHive hive, RegistryView view, string subKeyPath);
}

/// <summary>
/// Real implementation that reads from the Windows Registry.
/// </summary>
public class WindowsRegistryAccessor : IRegistryAccessor
{
    public string[] GetSubKeyNames(RegistryHive hive, RegistryView view, string subKeyPath)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        return subKey?.GetSubKeyNames() ?? [];
    }

    public string? GetStringValue(RegistryHive hive, RegistryView view, string subKeyPath, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        return subKey?.GetValue(valueName) as string;
    }

    public int? GetDwordValue(RegistryHive hive, RegistryView view, string subKeyPath, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        var value = subKey?.GetValue(valueName);
        return value is int intVal ? intVal : null;
    }

    public string[] GetValueNames(RegistryHive hive, RegistryView view, string subKeyPath)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var subKey = baseKey.OpenSubKey(subKeyPath);
        return subKey?.GetValueNames() ?? [];
    }
}
