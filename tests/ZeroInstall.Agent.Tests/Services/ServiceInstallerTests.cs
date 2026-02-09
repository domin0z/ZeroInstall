using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Agent.Tests.Services;

public class ServiceInstallerTests
{
    [Fact]
    public async Task InstallAsync_CallsScCreate()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("create")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "[SC] CreateService SUCCESS" });

        var installer = new ServiceInstaller(processRunner, NullLogger<ServiceInstaller>.Instance);

        var result = await installer.InstallAsync(@"C:\tools\zim-agent.exe", "mykey", 19850);

        result.Should().BeTrue();
        await processRunner.Received(1).RunAsync("sc.exe",
            Arg.Is<string>(s => s.Contains("create ZeroInstallAgent") && s.Contains("mykey") && s.Contains("19850")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UninstallAsync_CallsScStopThenDelete()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("stop")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });
        processRunner.RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("delete")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "[SC] DeleteService SUCCESS" });

        var installer = new ServiceInstaller(processRunner, NullLogger<ServiceInstaller>.Instance);

        var result = await installer.UninstallAsync();

        result.Should().BeTrue();
        await processRunner.Received(1).RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("stop")), Arg.Any<CancellationToken>());
        await processRunner.Received(1).RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("delete")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsInstalledAsync_ReturnsTrueWhenServiceExists()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("query")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "SERVICE_NAME: ZeroInstallAgent\n        TYPE               : 10  WIN32_OWN_PROCESS\n        STATE              : 1  STOPPED"
            });

        var installer = new ServiceInstaller(processRunner, NullLogger<ServiceInstaller>.Instance);

        var result = await installer.IsInstalledAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInstalledAsync_ReturnsFalseWhenServiceNotFound()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync("sc.exe", Arg.Is<string>(s => s.Contains("query")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 1060,
                StandardOutput = "",
                StandardError = "The specified service does not exist as an installed service."
            });

        var installer = new ServiceInstaller(processRunner, NullLogger<ServiceInstaller>.Instance);

        var result = await installer.IsInstalledAsync();
        result.Should().BeFalse();
    }
}
