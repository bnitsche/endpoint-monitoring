using System.Diagnostics;
using System.Net.NetworkInformation;
using EndpointMonitoring.Core.Providers;

namespace EndpointMonitoring.Core.Providers.Ping;

public class PingMonitoringProvider : IMonitoringProvider
{
    public string ProviderType => "ping";
    public string DisplayName => "Ping (ICMP)";
    public string Description => "Sends an ICMP ping to a host and checks reachability.";
    public string ConfigTemplate => """{"host":"192.168.1.1","timeoutMs":3000}""";

    public IReadOnlyList<ProviderConfigField> ConfigFields =>
    [
        new() { Key = "host",      Label = "Host / IP Address", FieldType = ProviderConfigFieldType.Text,   DefaultValue = "192.168.1.1", Required = true,  HelperText = "Hostname or IP address to ping" },
        new() { Key = "timeoutMs", Label = "Timeout (ms)",      FieldType = ProviderConfigFieldType.Number, DefaultValue = "3000",        Required = false, HelperText = "Ping timeout in milliseconds (default: 3000)" }
    ];

    public async Task<MonitoringCheckResult> CheckAsync(string providerConfig, CancellationToken cancellationToken = default)
    {
        if (!JsonConfigHelper.TryDeserialize<PingConfig>(providerConfig, out var cfg))
            return MonitoringCheckResult.Failure("Invalid config", "Could not parse provider configuration JSON.");

        var timeout = cfg.TimeoutMs > 0 ? cfg.TimeoutMs : 3000;

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(cfg.Host, timeout);

            if (reply.Status == IPStatus.Success)
                return MonitoringCheckResult.Success((int)reply.RoundtripTime, $"TTL={reply.Options?.Ttl}");

            return MonitoringCheckResult.Failure(reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return MonitoringCheckResult.Failure("Error", ex.Message);
        }
    }

    private sealed record PingConfig(
        [property: System.Text.Json.Serialization.JsonPropertyName("host")] string Host,
        [property: System.Text.Json.Serialization.JsonPropertyName("timeoutMs")] int TimeoutMs = 3000);
}
