namespace EndpointMonitoring.Web.Services;

/// <summary>In-process event bus that bridges the SignalR hub to Blazor components.</summary>
public interface IMonitoringUpdateNotifier
{
    /// <summary>Raised when a check for the given endpoint ID has completed.</summary>
    event Action<int>? OnEndpointChecked;

    /// <summary>Fires <see cref="OnEndpointChecked"/> for the given <paramref name="endpointId"/>.</summary>
    void NotifyEndpointChecked(int endpointId);
}

/// <summary>Default singleton implementation of <see cref="IMonitoringUpdateNotifier"/>.</summary>
public class MonitoringUpdateNotifier : IMonitoringUpdateNotifier
{
    /// <inheritdoc/>
    public event Action<int>? OnEndpointChecked;

    /// <inheritdoc/>
    public void NotifyEndpointChecked(int endpointId) => OnEndpointChecked?.Invoke(endpointId);
}
