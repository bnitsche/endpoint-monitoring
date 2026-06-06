namespace EndpointMonitoring.Web.Services;

/// <summary>
/// Singleton tracking the SignalR link to the monitoring service (the only client of the monitoring hub):
/// whether it is currently connected, since when, and when the last check signal arrived.
/// </summary>
public sealed class MonitoringServiceLinkRegistry
{
    private readonly object _lock = new();
    private int _connectionCount;

    /// <summary>When the current connection was established, or null while disconnected.</summary>
    public DateTime? ConnectedAt { get; private set; }

    /// <summary>When the last check-completed signal was received from the monitoring service.</summary>
    public DateTime? LastSignalAt { get; private set; }

    /// <summary>Whether the monitoring service is currently connected to the hub.</summary>
    public bool IsConnected
    {
        get { lock (_lock) return _connectionCount > 0; }
    }

    /// <summary>Raised whenever the connection state or last-signal timestamp changes.</summary>
    public event Action? OnChanged;

    /// <summary>Records a new hub connection. A counter is used because a reconnect may open before the old connection closes.</summary>
    public void MarkConnected()
    {
        lock (_lock)
        {
            _connectionCount++;
            if (_connectionCount == 1)
                ConnectedAt = DateTime.Now;
        }
        OnChanged?.Invoke();
    }

    /// <summary>Records a closed hub connection.</summary>
    public void MarkDisconnected()
    {
        lock (_lock)
        {
            if (_connectionCount > 0)
                _connectionCount--;
            if (_connectionCount == 0)
                ConnectedAt = null;
        }
        OnChanged?.Invoke();
    }

    /// <summary>Records that a check-completed signal was just received.</summary>
    public void MarkSignal()
    {
        LastSignalAt = DateTime.Now;
        OnChanged?.Invoke();
    }
}
