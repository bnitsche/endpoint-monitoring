namespace EndpointMonitoring.Core.Models;

/// <summary>A single recorded check result for a <see cref="MonitoredEndpoint"/>.</summary>
public class MonitoringResult
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the parent <see cref="MonitoredEndpoint"/>.</summary>
    public int EndpointId { get; set; }

    /// <summary>UTC time when the check was executed.</summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the check passed.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Round-trip time in milliseconds, or <see langword="null"/> when not applicable.</summary>
    public int? ResponseTimeMs { get; set; }

    /// <summary>Short human-readable status description (e.g. "OK" or an error message).</summary>
    public string? StatusMessage { get; set; }

    /// <summary>Extended diagnostic details returned by the provider.</summary>
    public string? Details { get; set; }

    /// <summary>Navigation property to the parent endpoint.</summary>
    public MonitoredEndpoint Endpoint { get; set; } = null!;
}
