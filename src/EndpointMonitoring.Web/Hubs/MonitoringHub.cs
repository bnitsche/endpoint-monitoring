using EndpointMonitoring.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace EndpointMonitoring.Web.Hubs;

/// <summary>SignalR hub that receives check-completed notifications from the monitoring service and relays them in-process.</summary>
public class MonitoringHub : Hub
{
    private readonly IMonitoringUpdateNotifier _notifier;

    /// <summary>Initialises the hub with the in-process update notifier.</summary>
    public MonitoringHub(IMonitoringUpdateNotifier notifier) => _notifier = notifier;

    /// <summary>Called by the monitoring service after each check.</summary>
    public Task NotifyCheckCompleted(int endpointId)
    {
        _notifier.NotifyEndpointChecked(endpointId);
        return Task.CompletedTask;
    }
}
