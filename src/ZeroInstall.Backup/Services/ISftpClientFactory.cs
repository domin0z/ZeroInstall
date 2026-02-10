using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Services;

/// <summary>
/// Factory for creating SFTP client instances from configuration.
/// </summary>
internal interface ISftpClientFactory
{
    /// <summary>
    /// Creates a new SFTP client wrapper configured with the given settings.
    /// The caller is responsible for connecting and disposing.
    /// </summary>
    ISftpClientWrapper Create(SftpTransportConfiguration config);
}
