using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using EndpointMonitoring.Core.Providers;

namespace EndpointMonitoring.Core.Providers.FritzBox;

/// <summary>
/// Checks Fritz!Box internet connectivity using the standard UPnP/IGD TR-064 profile.
/// Queries GetStatusInfo on the WANIPConnection service which works on all Fritz!Box models.
/// </summary>
public class FritzBoxMonitoringProvider : IMonitoringProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Initialises the provider with the shared HTTP client factory.</summary>
    public FritzBoxMonitoringProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public string ProviderType => "fritzbox";
    /// <inheritdoc/>
    public string DisplayName => "Fritz!Box Internet";
    /// <inheritdoc/>
    public string Description => "Checks internet connectivity status of a Fritz!Box via UPnP/IGD (TR-064).";
    /// <inheritdoc/>
    public string ConfigTemplate => """{"host":"fritz.box","port":49000,"timeoutSeconds":10}""";

    /// <inheritdoc/>
    public IReadOnlyList<ProviderConfigField> ConfigFields =>
    [
        new() { Key = "host",           Label = "Fritz!Box Host",    FieldType = ProviderConfigFieldType.Text,   DefaultValue = "fritz.box", Required = true,  HelperText = "Hostname or IP of your Fritz!Box (default: fritz.box)" },
        new() { Key = "port",           Label = "UPnP Port",         FieldType = ProviderConfigFieldType.Number, DefaultValue = "49000",     Required = false, HelperText = "UPnP port (default: 49000)" },
        new() { Key = "timeoutSeconds", Label = "Timeout (seconds)", FieldType = ProviderConfigFieldType.Number, DefaultValue = "10",        Required = false }
    ];

    /// <inheritdoc/>
    public async Task<MonitoringCheckResult> CheckAsync(string providerConfig, CancellationToken cancellationToken = default)
    {
        if (!JsonConfigHelper.TryDeserialize<FritzBoxConfig>(providerConfig, out var cfg))
            return MonitoringCheckResult.Failure("Invalid config", "Could not parse provider configuration JSON.");

        var rawHost = string.IsNullOrWhiteSpace(cfg.Host) ? "fritz.box" : cfg.Host.Trim();
        var port = cfg.Port > 0 ? cfg.Port : 49000;

        // If the user pasted a full URL (e.g. https://fritz.box:6656), extract only the host and port.
        string host;
        if (Uri.TryCreate(rawHost, UriKind.Absolute, out var parsedUri))
        {
            host = parsedUri.Host;
            if (parsedUri.Port > 0)
                port = parsedUri.Port;
        }
        else
        {
            host = rawHost;
        }

        var url = $"http://{host}:{port}/igdupnp/control/WANIPConn1";

        const string soapBody = """
            <?xml version="1.0" encoding="utf-8"?>
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"
                        s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
              <s:Body>
                <u:GetStatusInfo xmlns:u="urn:schemas-upnp-org:service:WANIPConnection:1"/>
              </s:Body>
            </s:Envelope>
            """;

        var client = _httpClientFactory.CreateClient("monitoring");
        client.Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 10);

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(soapBody, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "\"urn:schemas-upnp-org:service:WANIPConnection:1#GetStatusInfo\"");

            using var response = await client.SendAsync(request, cancellationToken);
            sw.Stop();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);

            XNamespace ns = "urn:schemas-upnp-org:service:WANIPConnection:1";
            var connectionStatus = doc.Descendants(ns + "NewConnectionStatus").FirstOrDefault()?.Value;
            var externalIp = doc.Descendants(ns + "NewExternalIPAddress").FirstOrDefault()?.Value;

            if (connectionStatus?.Equals("Connected", StringComparison.OrdinalIgnoreCase) == true)
                return MonitoringCheckResult.Success((int)sw.ElapsedMilliseconds, $"Connected, IP: {externalIp}");

            return MonitoringCheckResult.Failure($"Status: {connectionStatus ?? "Unknown"}");
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return MonitoringCheckResult.Failure("Timeout", $"No response from Fritz!Box at {host}:{port}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return MonitoringCheckResult.Failure("Error", ex.Message);
        }
    }

    private sealed record FritzBoxConfig(
        [property: System.Text.Json.Serialization.JsonPropertyName("host")] string Host = "fritz.box",
        [property: System.Text.Json.Serialization.JsonPropertyName("port")] int Port = 49000,
        [property: System.Text.Json.Serialization.JsonPropertyName("timeoutSeconds")] int TimeoutSeconds = 10);
}
