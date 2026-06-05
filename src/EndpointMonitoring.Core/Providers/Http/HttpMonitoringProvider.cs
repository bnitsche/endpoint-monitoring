using System.Diagnostics;
using EndpointMonitoring.Core.Providers;

namespace EndpointMonitoring.Core.Providers.Http;

/// <summary>Checks that an HTTP/HTTPS URL returns the expected status code.</summary>
public class HttpMonitoringProvider : IMonitoringProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Initialises the provider with the shared HTTP client factory.</summary>
    public HttpMonitoringProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public string ProviderType => "http";
    /// <inheritdoc/>
    public string DisplayName => "HTTP Endpoint";
    /// <inheritdoc/>
    public string Description => "Checks that an HTTP/HTTPS URL returns a successful status code.";
    /// <inheritdoc/>
    public string ConfigTemplate => """{"url":"https://example.com","expectedStatusCode":200,"timeoutSeconds":10}""";

    /// <inheritdoc/>
    public IReadOnlyList<ProviderConfigField> ConfigFields =>
    [
        new() { Key = "url",                Label = "URL",                   FieldType = ProviderConfigFieldType.Url,    DefaultValue = "https://",    Required = true,  HelperText = "Full URL to check, e.g. https://example.com" },
        new() { Key = "expectedStatusCode", Label = "Expected Status Code",  FieldType = ProviderConfigFieldType.Number, DefaultValue = "200",         Required = false, HelperText = "HTTP status code that counts as success (default: 200)" },
        new() { Key = "timeoutSeconds",     Label = "Timeout (seconds)",     FieldType = ProviderConfigFieldType.Number, DefaultValue = "10",          Required = false }
    ];

    /// <inheritdoc/>
    public async Task<MonitoringCheckResult> CheckAsync(string providerConfig, CancellationToken cancellationToken = default)
    {
        if (!JsonConfigHelper.TryDeserialize<HttpConfig>(providerConfig, out var cfg))
            return MonitoringCheckResult.Failure("Invalid config", "Could not parse provider configuration JSON.");

        var client = _httpClientFactory.CreateClient("monitoring");
        client.Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 10);

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(cfg.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sw.Stop();

            var expectedCode = cfg.ExpectedStatusCode > 0 ? cfg.ExpectedStatusCode : 200;
            if ((int)response.StatusCode == expectedCode)
                return MonitoringCheckResult.Success((int)sw.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}");

            return MonitoringCheckResult.Failure($"HTTP {(int)response.StatusCode}", $"Expected {expectedCode}");
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return MonitoringCheckResult.Failure("Timeout", $"No response within {client.Timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return MonitoringCheckResult.Failure("Error", ex.Message);
        }
    }

    private sealed record HttpConfig(
        [property: System.Text.Json.Serialization.JsonPropertyName("url")] string Url,
        [property: System.Text.Json.Serialization.JsonPropertyName("expectedStatusCode")] int ExpectedStatusCode = 200,
        [property: System.Text.Json.Serialization.JsonPropertyName("timeoutSeconds")] int TimeoutSeconds = 10);
}
