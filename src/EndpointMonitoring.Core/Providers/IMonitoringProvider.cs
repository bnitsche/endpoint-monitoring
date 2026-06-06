namespace EndpointMonitoring.Core.Providers;

/// <summary>Contract for a pluggable endpoint check provider.</summary>
public interface IMonitoringProvider
{
    /// <summary>Unique string identifier used in persisted configuration (e.g. "http", "ping").</summary>
    string ProviderType { get; }

    /// <summary>Human-readable name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Short description of what this provider checks.</summary>
    string Description { get; }

    /// <summary>
    /// Optional usage note shown as an info box in the endpoint dialog when this provider is selected
    /// (e.g. prerequisites or limitations). Null if the provider has no special requirements.
    /// </summary>
    string? UsageNote => null;

    /// <summary>Structured field descriptors used to build the config form in the UI.</summary>
    IReadOnlyList<ProviderConfigField> ConfigFields { get; }

    /// <summary>Returns a JSON schema / example config string — used as fallback.</summary>
    string ConfigTemplate { get; }

    /// <summary>Performs the check and returns the result.</summary>
    Task<MonitoringCheckResult> CheckAsync(string providerConfig, CancellationToken cancellationToken = default);
}
