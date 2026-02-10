using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Services;
using ZeroInstall.WinPE.Infrastructure;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE.Tests.Infrastructure;

public class WinPeHostTests : IDisposable
{
    private readonly string _originalDir;

    public WinPeHostTests()
    {
        // Save and set base directory so logs directory can be created in temp
        _originalDir = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
    }

    [Fact]
    public void BuildHost_ReturnsValidHost()
    {
        using var host = WinPeHost.BuildHost(verbose: false);

        host.Should().NotBeNull();
    }

    [Fact]
    public void BuildHost_ServicesResolve()
    {
        using var host = WinPeHost.BuildHost(verbose: true);

        var imageBrowser = host.Services.GetService<ImageBrowserService>();
        var orchestrator = host.Services.GetService<RestoreOrchestrator>();
        var diskEnum = host.Services.GetService<DiskEnumerationService>();
        var diskCloner = host.Services.GetService<IDiskCloner>();

        imageBrowser.Should().NotBeNull();
        orchestrator.Should().NotBeNull();
        diskEnum.Should().NotBeNull();
        diskCloner.Should().NotBeNull();
    }
}
