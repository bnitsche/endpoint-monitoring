namespace EndpointMonitoring.Core.Models;

public class MonitoredEndpoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string ProviderConfig { get; set; } = "{}";
    public int IntervalSeconds { get; set; } = 60;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MonitoringResult> Results { get; set; } = [];
}
