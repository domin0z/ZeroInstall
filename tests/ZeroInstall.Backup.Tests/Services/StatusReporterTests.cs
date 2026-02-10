using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;
using ZeroInstall.Backup.Services;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Backup.Tests.Services;

public class StatusReporterTests
{
    private readonly ISftpClientWrapper _mockClient;
    private readonly ISftpClientFactory _mockFactory;
    private readonly StatusReporter _reporter;

    public StatusReporterTests()
    {
        _mockClient = Substitute.For<ISftpClientWrapper>();
        _mockFactory = Substitute.For<ISftpClientFactory>();
        _mockFactory.Create(Arg.Any<SftpTransportConfiguration>()).Returns(_mockClient);

        _reporter = new StatusReporter(_mockFactory, NullLogger<StatusReporter>.Instance);
    }

    [Fact]
    public async Task ReportStatusAsync_UploadsStatusFile()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        var status = new BackupStatus
        {
            CustomerId = "cust-001",
            MachineName = "DESKTOP-ABC",
            LastRunResult = BackupRunResultType.Success
        };

        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        await _reporter.ReportStatusAsync(config, status);

        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            Arg.Is<string>(s => s.Contains("backup-status.json")));
    }

    [Fact]
    public async Task SubmitRestoreRequestAsync_UploadsRequestFile()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        var request = new RestoreRequest
        {
            CustomerId = "cust-001",
            Scope = RestoreScope.Partial,
            Message = "Need my docs back"
        };

        _mockClient.Exists(Arg.Any<string>()).Returns(true);

        await _reporter.SubmitRestoreRequestAsync(config, request);

        _mockClient.Received().UploadFile(
            Arg.Any<Stream>(),
            Arg.Is<string>(s => s.Contains("restore-request.json")));
    }

    [Fact]
    public async Task ReportStatusAsync_HandlesSftpError_Gracefully()
    {
        var config = new BackupConfiguration
        {
            CustomerId = "cust-001",
            NasConnection = { RemoteBasePath = "/backups" }
        };

        _mockFactory.Create(Arg.Any<SftpTransportConfiguration>())
            .Returns(_ => throw new InvalidOperationException("Connection failed"));

        // Should not throw
        await _reporter.ReportStatusAsync(config, new BackupStatus());
    }
}
