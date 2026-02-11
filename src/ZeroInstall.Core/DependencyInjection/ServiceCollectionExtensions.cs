using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.DependencyInjection;

/// <summary>
/// Extension methods to register ZeroInstall.Core services into the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core services (infrastructure, discovery, migration, profile/settings).
    /// IJobLogger and IProfileManager are NOT registered here — they require runtime paths
    /// and must be registered by the host project.
    /// </summary>
    public static IServiceCollection AddZeroInstallCore(this IServiceCollection services)
    {
        // Infrastructure (singletons)
        services.AddSingleton<IRegistryAccessor, WindowsRegistryAccessor>();
        services.AddSingleton<IFileSystemAccessor, WindowsFileSystemAccessor>();
        services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
        services.AddSingleton<IBitLockerService, BitLockerService>();
        services.AddSingleton<IFirmwareService, FirmwareService>();

        // Discovery (transient)
        services.AddTransient<ApplicationDiscoveryService>();
        services.AddTransient<UserProfileDiscoveryService>();
        services.AddTransient<SystemSettingsDiscoveryService>();
        services.AddTransient<IDiscoveryService, DiscoveryService>();

        // Tier 1 — Package-based migration
        services.AddTransient<AppDataCaptureHelper>();
        services.AddTransient<IPackageMigrator, PackageMigratorService>();

        // Tier 2 — Registry + file capture
        services.AddTransient<RegistryCaptureService>();
        services.AddTransient<FileCaptureService>();
        services.AddTransient<IRegistryMigrator, RegistryFileMigratorService>();

        // Tier 3 — Full disk clone
        services.AddTransient<IDiskCloner, DiskClonerService>();

        // Disk enumeration + driver injection (reusable across CLI/App/WinPE)
        services.AddTransient<DiskEnumerationService>();
        services.AddTransient<DriverInjectionService>();

        // Profile & settings migration
        services.AddTransient<ProfileTransferService>();
        services.AddTransient<BrowserDataService>();
        services.AddTransient<EmailDataService>();
        services.AddTransient<SystemSettingsReplayService>();
        services.AddTransient<IUserPathRemapper, UserPathRemapService>();
        services.AddTransient<IUserAccountManager, UserAccountService>();
        services.AddTransient<ProfileSettingsMigratorService>();

        return services;
    }
}
