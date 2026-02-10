using Renci.SshNet;

namespace ZeroInstall.Core.Transport;

/// <summary>
/// SSH.NET implementation of <see cref="ISftpClientWrapper"/>.
/// Supports password and/or private key authentication.
/// </summary>
public sealed class SftpClientWrapper : ISftpClientWrapper
{
    private readonly SftpClient _client;

    public SftpClientWrapper(
        string host,
        int port,
        string username,
        string? password = null,
        string? privateKeyPath = null,
        string? privateKeyPassphrase = null)
    {
        var authMethods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(password))
            authMethods.Add(new PasswordAuthenticationMethod(username, password));

        if (!string.IsNullOrEmpty(privateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(privateKeyPassphrase)
                ? new PrivateKeyFile(privateKeyPath)
                : new PrivateKeyFile(privateKeyPath, privateKeyPassphrase);
            authMethods.Add(new PrivateKeyAuthenticationMethod(username, keyFile));
        }

        if (authMethods.Count == 0)
            throw new ArgumentException("At least one authentication method (password or private key) must be provided.");

        var connectionInfo = new ConnectionInfo(host, port, username, authMethods.ToArray());
        _client = new SftpClient(connectionInfo);
    }

    public bool IsConnected => _client.IsConnected;

    public void Connect() => _client.Connect();

    public void Disconnect() => _client.Disconnect();

    public bool Exists(string path) => _client.Exists(path);

    public void CreateDirectory(string path) => _client.CreateDirectory(path);

    public void DeleteFile(string path) => _client.DeleteFile(path);

    public IEnumerable<SftpFileInfo> ListDirectory(string path)
    {
        return _client.ListDirectory(path)
            .Where(f => f.Name != "." && f.Name != "..")
            .Select(f => new SftpFileInfo(
                f.Name,
                f.FullName,
                f.IsDirectory,
                f.Length,
                f.LastWriteTimeUtc));
    }

    public Stream OpenRead(string path) => _client.OpenRead(path);

    public Stream Create(string path) => _client.Create(path);

    public void UploadFile(Stream input, string path, Action<ulong>? uploadCallback = null)
        => _client.UploadFile(input, path, uploadCallback);

    public void DownloadFile(string path, Stream output, Action<ulong>? downloadCallback = null)
        => _client.DownloadFile(path, output, downloadCallback);

    public long GetFileSize(string path)
    {
        var attrs = _client.GetAttributes(path);
        return attrs.Size;
    }

    public void RenameFile(string oldPath, string newPath) => _client.RenameFile(oldPath, newPath);

    public void Dispose() => _client.Dispose();
}
