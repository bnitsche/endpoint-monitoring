namespace EndpointMonitoring.Core.Models;

/// <summary>A configured endpoint that the monitoring service checks on a recurring schedule.</summary>
public class MonitoredEndpoint
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Identifier of the <see cref="Providers.IMonitoringProvider"/> to use (e.g. "http", "ping").</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>Provider-specific configuration as a JSON string.</summary>
    public string ProviderConfig { get; set; } = "{}";

    /// <summary>How frequently (in seconds) to run a check.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>When false, the monitoring service skips this endpoint entirely.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>UTC timestamp when this endpoint was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last configuration change.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set to UTC time when the failure alert email was sent for the current outage.
    /// Null means no alert is active. Cleared on recovery or admin acknowledgement.
    /// </summary>
    public DateTime? AlertSentAt { get; set; }

    /// <summary>All check results recorded for this endpoint.</summary>
    public ICollection<MonitoringResult> Results { get; set; } = [];
}
