namespace ZeroInstall.Core.Transport;

/// <summary>
/// Testability abstraction over SSH.NET's SftpClient.
/// Allows unit tests to mock SFTP operations without a real server.
/// </summary>
public interface ISftpClientWrapper : IDisposable
{
    bool IsConnected { get; }
    void Connect();
    void Disconnect();
    bool Exists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    IEnumerable<SftpFileInfo> ListDirectory(string path);
    Stream OpenRead(string path);
    Stream Create(string path);
    void UploadFile(Stream input, string path, Action<ulong>? uploadCallback = null);
    void DownloadFile(string path, Stream output, Action<ulong>? downloadCallback = null);
    long GetFileSize(string path);
    void RenameFile(string oldPath, string newPath);
}

/// <summary>
/// Information about a remote file or directory on an SFTP server.
/// </summary>
public record SftpFileInfo(string Name, string FullName, bool IsDirectory, long Length, DateTime LastWriteTimeUtc);
