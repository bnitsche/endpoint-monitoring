using Microsoft.AspNetCore.SignalR.Client;

namespace EndpointMonitoring.MonitoringService.SignalR;

/// <summary>Hosted service that maintains a SignalR connection to the web project's hub and pushes check-completed events.</summary>
public class MonitoringHubClient : IHostedService, IAsyncDisposable
{
    private readonly string _hubUrl;
    private readonly ILogger<MonitoringHubClient> _logger;
    private HubConnection? _connection;
    private CancellationTokenSource? _cts;

    /// <summary>Resolves the hub URL from Aspire service discovery or the <c>WebsiteUrl</c> config key.</summary>
    public MonitoringHubClient(IConfiguration config, ILogger<MonitoringHubClient> logger)
    {
        _logger = logger;

        // Under Aspire, WithReference(web) injects the web project URL as env vars.
        // The .NET config system maps  services__webfrontend__https__0  →  services:webfrontend:https:0
        // so we must use colon-separated keys here, NOT the raw env-var names.
        var baseUrl = (config["services:webfrontend:https:0"]
                    ?? config["services:webfrontend:http:0"]
                    ?? config["WebsiteUrl"])?.TrimEnd('/') ?? string.Empty;

        _hubUrl = string.IsNullOrEmpty(baseUrl) ? string.Empty : $"{baseUrl}/hubs/monitoring";
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_hubUrl))
        {
            _logger.LogWarning("No hub URL configured (WebsiteUrl missing) — real-time push disabled.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Monitoring hub target: {Url}.", _hubUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, opts =>
            {
                opts.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            })
            .WithAutomaticReconnect()   // handles drops after a successful connection
            .Build();

        _connection.Reconnected += id =>
        {
            _logger.LogInformation("Reconnected to monitoring hub (connectionId={Id}).", id);
            return Task.CompletedTask;
        };
        _connection.Closed += ex =>
        {
            // WithAutomaticReconnect gave up — our retry loop will pick it back up.
            _logger.LogWarning("Monitoring hub connection closed. {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ConnectWithRetryAsync(_cts.Token);

        return Task.CompletedTask;
    }

    // Continuously retries StartAsync until the connection is established.
    // WithAutomaticReconnect handles fast reconnects after an established connection drops;
    // this loop handles the initial connect and the case where AutoReconnect gives up.
    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_connection!.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _connection.StartAsync(cancellationToken);
                    _logger.LogInformation("Connected to monitoring hub.");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Hub connection attempt failed — retrying in 30 s.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_connection is not null)
            await _connection.StopAsync(cancellationToken);
    }

    /// <summary>Notifies the hub that a check for <paramref name="endpointId"/> has completed. No-ops if not connected.</summary>
    public async Task NotifyCheckCompletedAsync(int endpointId)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("NotifyCheckCompleted", endpointId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push check-completed event for endpoint {Id}.", endpointId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
