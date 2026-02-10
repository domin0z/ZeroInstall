namespace ZeroInstall.Core.Models;

/// <summary>
/// Configuration for SFTP-based transport to a remote NAS or server.
/// </summary>
public class SftpTransportConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
    public string RemoteBasePath { get; set; } = "/backups/zim";
    public string? EncryptionPassphrase { get; set; }
    public bool CompressBeforeUpload { get; set; } = true;
}
