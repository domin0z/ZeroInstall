namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Abstraction over filesystem access for testability.
/// </summary>
public interface IFileSystemAccessor
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string[] GetDirectories(string path);
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    long GetDirectorySize(string path);
    long GetFileSize(string path);
}

/// <summary>
/// Real implementation that reads from the Windows filesystem.
/// </summary>
public class WindowsFileSystemAccessor : IFileSystemAccessor
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public string[] GetDirectories(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (UnauthorizedAccessException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        try { return Directory.GetFiles(path, searchPattern, searchOption); }
        catch (UnauthorizedAccessException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }

    public long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return f.Length; }
                    catch { return 0L; }
                });
        }
        catch { return 0; }
    }

    public long GetFileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }
}
