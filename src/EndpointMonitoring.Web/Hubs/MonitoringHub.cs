using EndpointMonitoring.Core.Logging;
using EndpointMonitoring.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace EndpointMonitoring.Web.Hubs;

/// <summary>SignalR hub that receives check-completed notifications and log entries from the monitoring service and relays them in-process.</summary>
public class MonitoringHub : Hub
{
    private readonly IMonitoringUpdateNotifier _notifier;
    private readonly LogStreamService _logStream;
    private readonly MonitoringServiceLinkRegistry _link;

    /// <summary>Initialises the hub with the in-process update notifier, log stream and link registry.</summary>
    public MonitoringHub(IMonitoringUpdateNotifier notifier, LogStreamService logStream,
                         MonitoringServiceLinkRegistry link)
    {
        _notifier = notifier;
        _logStream = logStream;
        _link = link;
    }

    /// <summary>The only client of this hub is the monitoring service, so any connection is tracked as its link.</summary>
    public override async Task OnConnectedAsync()
    {
        _link.MarkConnected();
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _link.MarkDisconnected();
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Called by the monitoring service after each check.</summary>
    public Task NotifyCheckCompleted(int endpointId)
    {
        _link.MarkSignal();
        _notifier.NotifyEndpointChecked(endpointId);
        return Task.CompletedTask;
    }

    /// <summary>Called by the monitoring service to stream a batch of log entries to the web UI.</summary>
    public Task PublishLogs(LogEntry[] entries)
    {
        foreach (var entry in entries)
            _logStream.Write(entry);
        return Task.CompletedTask;
    }
}
