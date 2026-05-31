using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Models;
using EndpointMonitoring.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.MonitoringService;

public class EndpointMonitoringWorker : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly MonitoringProviderRegistry _registry;
    private readonly ILogger<EndpointMonitoringWorker> _logger;

    // Tracks the next scheduled check time per endpoint id
    private readonly Dictionary<int, DateTimeOffset> _nextCheck = [];

    public EndpointMonitoringWorker(
        IDbContextFactory<AppDbContext> dbFactory,
        MonitoringProviderRegistry registry,
        ILogger<EndpointMonitoringWorker> logger)
    {
        _dbFactory = dbFactory;
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Endpoint monitoring worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueChecksAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in monitoring loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("Endpoint monitoring worker stopped.");
    }

    private async Task RunDueChecksAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var endpoints = await db.Endpoints
            .Where(e => e.IsEnabled)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var tasks = new List<Task>();

        foreach (var endpoint in endpoints)
        {
            if (_nextCheck.TryGetValue(endpoint.Id, out var next) && now < next)
                continue;

            tasks.Add(CheckEndpointAsync(endpoint, cancellationToken));
            _nextCheck[endpoint.Id] = now.AddSeconds(endpoint.IntervalSeconds);
        }

        // Remove stale entries for deleted endpoints
        var activeIds = endpoints.Select(e => e.Id).ToHashSet();
        foreach (var key in _nextCheck.Keys.Where(k => !activeIds.Contains(k)).ToList())
            _nextCheck.Remove(key);

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task CheckEndpointAsync(MonitoredEndpoint endpoint, CancellationToken cancellationToken)
    {
        var provider = _registry.GetByType(endpoint.ProviderType);
        if (provider is null)
        {
            _logger.LogWarning("No provider found for type '{ProviderType}' (endpoint: {EndpointName}).",
                endpoint.ProviderType, endpoint.Name);
            return;
        }

        _logger.LogDebug("Checking endpoint '{EndpointName}' via {ProviderType}.", endpoint.Name, provider.ProviderType);

        MonitoringCheckResult result;
        try
        {
            result = await provider.CheckAsync(endpoint.ProviderConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            result = MonitoringCheckResult.Failure("Unhandled exception", ex.Message);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Results.Add(new MonitoringResult
        {
            EndpointId = endpoint.Id,
            CheckedAt = DateTime.UtcNow,
            IsSuccess = result.IsSuccess,
            ResponseTimeMs = result.ResponseTimeMs,
            StatusMessage = result.StatusMessage,
            Details = result.Details
        });
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Endpoint '{Name}': {Status} ({ResponseTimeMs}ms) – {Message}",
            endpoint.Name,
            result.IsSuccess ? "OK" : "FAIL",
            result.ResponseTimeMs,
            result.StatusMessage);
    }
}
