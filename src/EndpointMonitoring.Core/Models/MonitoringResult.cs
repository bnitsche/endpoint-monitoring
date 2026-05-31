namespace EndpointMonitoring.Core.Models;

public class MonitoringResult
{
    public long Id { get; set; }
    public int EndpointId { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string? StatusMessage { get; set; }
    public string? Details { get; set; }

    public MonitoredEndpoint Endpoint { get; set; } = null!;
}
