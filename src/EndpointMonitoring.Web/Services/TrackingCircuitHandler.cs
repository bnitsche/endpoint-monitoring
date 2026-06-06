using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace EndpointMonitoring.Web.Services;

/// <summary>
/// Scoped circuit handler that registers every browser circuit in the <see cref="ConnectedClientRegistry"/>
/// with user identity and remote IP, and keeps its connection state up to date.
/// </summary>
public sealed class TrackingCircuitHandler : CircuitHandler
{
    private readonly ConnectedClientRegistry _registry;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly string _remoteIp;

    /// <summary>Captures the remote IP from the circuit-establishing request (HttpContext is only available at that point).</summary>
    public TrackingCircuitHandler(ConnectedClientRegistry registry,
                                  AuthenticationStateProvider authStateProvider,
                                  IHttpContextAccessor httpContextAccessor)
    {
        _registry = registry;
        _authStateProvider = authStateProvider;
        _remoteIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <inheritdoc />
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        var userName = user.Identity?.IsAuthenticated == true
            ? user.Identity.Name ?? "(unknown)"
            : "(anonymous)";
        var role = user.FindFirst(ClaimTypes.Role)?.Value;

        _registry.Add(new ConnectedClient(circuit.Id, userName, role, _remoteIp,
                                          DateTime.Now, ClientConnectionState.Connected));
    }

    /// <inheritdoc />
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _registry.SetState(circuit.Id, ClientConnectionState.Connected);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _registry.SetState(circuit.Id, ClientConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _registry.Remove(circuit.Id);
        return Task.CompletedTask;
    }
}
