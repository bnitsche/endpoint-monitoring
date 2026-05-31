using Microsoft.Extensions.DependencyInjection;

namespace EndpointMonitoring.Core.Providers;

public class MonitoringProviderRegistry
{
    private readonly IEnumerable<IMonitoringProvider> _providers;

    public MonitoringProviderRegistry(IEnumerable<IMonitoringProvider> providers)
    {
        _providers = providers;
    }

    public IReadOnlyList<IMonitoringProvider> GetAll() => _providers.ToList().AsReadOnly();

    public IMonitoringProvider? GetByType(string providerType) =>
        _providers.FirstOrDefault(p => p.ProviderType.Equals(providerType, StringComparison.OrdinalIgnoreCase));
}
