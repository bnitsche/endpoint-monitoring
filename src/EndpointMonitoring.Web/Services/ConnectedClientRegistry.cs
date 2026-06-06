using System.Collections.Concurrent;

namespace EndpointMonitoring.Web.Services;

/// <summary>Connection state of a tracked browser circuit.</summary>
public enum ClientConnectionState
{
    /// <summary>The circuit has an active SignalR connection.</summary>
    Connected,

    /// <summary>The connection dropped but the circuit is still retained (client may reconnect).</summary>
    Reconnecting
}

/// <summary>A connected browser session (Blazor circuit) with its user identity and origin.</summary>
public sealed record ConnectedClient(
    string CircuitId,
    string UserName,
    string? Role,
    string RemoteIp,
    DateTime ConnectedAt,
    ClientConnectionState State)
{
    /// <summary>Whether the session belongs to a signed-in user (anonymous circuits come from the login page).</summary>
    public bool IsAuthenticated => Role is not null || UserName != "(anonymous)";
}

/// <summary>Singleton registry of all currently connected browser circuits, fed by <see cref="TrackingCircuitHandler"/>.</summary>
public sealed class ConnectedClientRegistry
{
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();

    /// <summary>Raised whenever a client is added, removed or changes connection state.</summary>
    public event Action? OnChanged;

    /// <summary>Returns a snapshot of all currently tracked clients.</summary>
    public IReadOnlyCollection<ConnectedClient> Snapshot() => _clients.Values.ToArray();

    /// <summary>Adds or replaces the client for its circuit id.</summary>
    public void Add(ConnectedClient client)
    {
        _clients[client.CircuitId] = client;
        OnChanged?.Invoke();
    }

    /// <summary>Updates the connection state of the given circuit, if tracked.</summary>
    public void SetState(string circuitId, ClientConnectionState state)
    {
        if (_clients.TryGetValue(circuitId, out var client) && client.State != state)
        {
            _clients[circuitId] = client with { State = state };
            OnChanged?.Invoke();
        }
    }

    /// <summary>Removes the client for the given circuit id, if tracked.</summary>
    public void Remove(string circuitId)
    {
        if (_clients.TryRemove(circuitId, out _))
            OnChanged?.Invoke();
    }
}
