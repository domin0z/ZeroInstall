using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZeroInstall.Core.DependencyInjection;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Services;

namespace ZeroInstall.CLI.Tests.DependencyInjection;

public class ServiceRegistrationTests
{
    private readonly IServiceProvider _provider;

    public ServiceRegistrationTests()
    {
        var services = new ServiceCollection();
        services.AddZeroInstallCore();
        services.AddLogging();
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void Infrastructure_Resolves()
    {
        _provider.GetRequiredService<IRegistryAccessor>().Should().NotBeNull();
        _provider.GetRequiredService<IFileSystemAccessor>().Should().NotBeNull();
        _provider.GetRequiredService<IProcessRunner>().Should().NotBeNull();
    }

    [Fact]
    public void DiscoveryService_Resolves()
    {
        _provider.GetRequiredService<IDiscoveryService>().Should().NotBeNull();
    }

    [Fact]
    public void PackageMigrator_Resolves()
    {
        _provider.GetRequiredService<IPackageMigrator>().Should().NotBeNull();
    }

    [Fact]
    public void RegistryMigrator_Resolves()
    {
        _provider.GetRequiredService<IRegistryMigrator>().Should().NotBeNull();
    }

    [Fact]
    public void DiskCloner_Resolves()
    {
        _provider.GetRequiredService<IDiskCloner>().Should().NotBeNull();
    }

    [Fact]
    public void ProfileSettingsMigrator_Resolves()
    {
        _provider.GetRequiredService<IUserPathRemapper>().Should().NotBeNull();
        _provider.GetRequiredService<IUserAccountManager>().Should().NotBeNull();
        _provider.GetRequiredService<ProfileSettingsMigratorService>().Should().NotBeNull();
    }
}
