namespace EndpointMonitoring.Core.Providers;

public class MonitoringCheckResult
{
    public bool IsSuccess { get; init; }
    public int? ResponseTimeMs { get; init; }
    public string? StatusMessage { get; init; }
    public string? Details { get; init; }

    public static MonitoringCheckResult Success(int responseTimeMs, string? details = null) =>
        new() { IsSuccess = true, ResponseTimeMs = responseTimeMs, StatusMessage = "OK", Details = details };

    public static MonitoringCheckResult Failure(string message, string? details = null) =>
        new() { IsSuccess = false, StatusMessage = message, Details = details };
}
