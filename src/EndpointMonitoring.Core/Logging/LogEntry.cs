using Microsoft.Extensions.Logging;

namespace EndpointMonitoring.Core.Logging;

/// <summary>A single log entry streamed in real time from one of the services to the web UI.</summary>
public sealed record LogEntry(
    DateTime Timestamp,
    string ServiceName,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception)
{
    /// <summary>Source name used for entries originating from the web application.</summary>
    public const string WebServiceName = "Web";

    /// <summary>Source name used for entries originating from the monitoring service.</summary>
    public const string MonitoringServiceName = "Monitoring Service";
}
