namespace EndpointMonitoring.Core.Providers;

/// <summary>The outcome of a single provider check execution.</summary>
public class MonitoringCheckResult
{
    /// <summary>Whether the check passed.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Round-trip time in milliseconds, or <see langword="null"/> when not measured.</summary>
    public int? ResponseTimeMs { get; init; }

    /// <summary>Short human-readable status string.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Extended diagnostic information from the provider.</summary>
    public string? Details { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static MonitoringCheckResult Success(int responseTimeMs, string? details = null) =>
        new() { IsSuccess = true, ResponseTimeMs = responseTimeMs, StatusMessage = "OK", Details = details };

    /// <summary>Creates a failure result.</summary>
    public static MonitoringCheckResult Failure(string message, string? details = null) =>
        new() { IsSuccess = false, StatusMessage = message, Details = details };
}
