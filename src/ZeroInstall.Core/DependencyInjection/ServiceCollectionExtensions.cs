using Microsoft.Extensions.DependencyInjection;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.DependencyInjection;

/// <summary>
/// Extension methods to register ZeroInstall.Core services into the DI container.
/// Implementations are registered by the host projects (App, Agent, CLI).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core service interfaces. Call this from the host's startup,
    /// then register concrete implementations for each interface.
    /// </summary>
    public static IServiceCollection AddZeroInstallCore(this IServiceCollection services)
    {
        // Core services are registered by the consuming host project
        // which provides the concrete implementations.
        // This method serves as the central registration point and
        // can be extended with default/shared registrations.

        return services;
    }
}
