namespace EndpointMonitoring.Core.Providers;

public interface IMonitoringProvider
{
    string ProviderType { get; }
    string DisplayName { get; }
    string Description { get; }

    /// <summary>Structured field descriptors used to build the config form in the UI.</summary>
    IReadOnlyList<ProviderConfigField> ConfigFields { get; }

    /// <summary>Returns a JSON schema / example config string — used as fallback.</summary>
    string ConfigTemplate { get; }

    Task<MonitoringCheckResult> CheckAsync(string providerConfig, CancellationToken cancellationToken = default);
}
