using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Backup.Services;
using ZeroInstall.Core.Discovery;
namespace ZeroInstall.Backup.Tests.Services;

public class BackupServiceInstallerTests
{
    private readonly IProcessRunner _mockRunner;
    private readonly BackupServiceInstaller _installer;

    public BackupServiceInstallerTests()
    {
        _mockRunner = Substitute.For<IProcessRunner>();
        _installer = new BackupServiceInstaller(_mockRunner, NullLogger<BackupServiceInstaller>.Instance);
    }

    [Fact]
    public async Task InstallAsync_CallsScExeCreate()
    {
        _mockRunner.RunAsync("sc.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "SUCCESS" });

        var result = await _installer.InstallAsync(@"C:\tools\zim-backup.exe", @"C:\config\backup.json");

        result.Should().BeTrue();
        await _mockRunner.Received().RunAsync("sc.exe",
            Arg.Is<string>(s => s.Contains("create ZeroInstallBackup")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UninstallAsync_StopsThenDeletes()
    {
        _mockRunner.RunAsync("sc.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "SUCCESS" });

        var result = await _installer.UninstallAsync();

        result.Should().BeTrue();
        await _mockRunner.Received().RunAsync("sc.exe",
            Arg.Is<string>(s => s.Contains("stop ZeroInstallBackup")),
            Arg.Any<CancellationToken>());
        await _mockRunner.Received().RunAsync("sc.exe",
            Arg.Is<string>(s => s.Contains("delete ZeroInstallBackup")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsInstalledAsync_ReturnsTrueWhenFound()
    {
        _mockRunner.RunAsync("sc.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "SERVICE_NAME: ZeroInstallBackup\r\n    STATE : 4  RUNNING"
            });

        var installed = await _installer.IsInstalledAsync();

        installed.Should().BeTrue();
    }

    [Fact]
    public async Task IsInstalledAsync_ReturnsFalseWhenNotFound()
    {
        _mockRunner.RunAsync("sc.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "FAILED 1060" });

        var installed = await _installer.IsInstalledAsync();

        installed.Should().BeFalse();
    }
}
