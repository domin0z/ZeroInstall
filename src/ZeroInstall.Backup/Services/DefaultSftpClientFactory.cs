using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Creates real SftpClientWrapper instances from configuration.
/// </summary>
internal class DefaultSftpClientFactory : ISftpClientFactory
{
    public ISftpClientWrapper Create(SftpTransportConfiguration config)
    {
        return new SftpClientWrapper(
            config.Host,
            config.Port,
            config.Username,
            config.Password,
            config.PrivateKeyPath,
            config.PrivateKeyPassphrase);
    }
}
