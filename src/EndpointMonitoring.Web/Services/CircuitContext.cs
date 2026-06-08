namespace EndpointMonitoring.Web.Services;

/// <summary>
/// Circuit-scoped bridge that exposes the current circuit's id to components. It lets a component
/// update the <see cref="ConnectedClientRegistry"/> entry that <see cref="TrackingCircuitHandler"/>
/// created for the same circuit — used to backfill the client IP captured during the initial HTTP
/// request (the circuit's own WebSocket request has no remote IP under IIS in-process hosting).
/// </summary>
public sealed class CircuitContext
{
    /// <summary>The current circuit's id, or null before the circuit has opened.</summary>
    public string? CircuitId { get; set; }
}
