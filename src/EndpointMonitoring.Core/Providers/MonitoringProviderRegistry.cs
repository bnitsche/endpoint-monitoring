using Microsoft.Extensions.DependencyInjection;

namespace EndpointMonitoring.Core.Providers;

/// <summary>Holds all registered <see cref="IMonitoringProvider"/> instances and exposes lookup helpers.</summary>
public class MonitoringProviderRegistry
{
    private readonly IEnumerable<IMonitoringProvider> _providers;

    /// <summary>Initialises the registry with the DI-resolved provider collection.</summary>
    public MonitoringProviderRegistry(IEnumerable<IMonitoringProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>Returns all registered providers.</summary>
    public IReadOnlyList<IMonitoringProvider> GetAll() => _providers.ToList().AsReadOnly();

    /// <summary>Returns the provider matching <paramref name="providerType"/> (case-insensitive), or <see langword="null"/>.</summary>
    public IMonitoringProvider? GetByType(string providerType) =>
        _providers.FirstOrDefault(p => p.ProviderType.Equals(providerType, StringComparison.OrdinalIgnoreCase));
}
