using System.Security.Claims;
using EndpointMonitoring.Core.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Web.Auth;

/// <summary>
/// Periodically re-validates the cookie-backed principal against the database so a user who
/// is disabled, deleted, or has had their role changed loses access within the interval.
/// </summary>
public sealed class CookieRevalidatingAuthStateProvider(
    ILoggerFactory loggerFactory,
    IDbContextFactory<AppDbContext> dbFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    /// <inheritdoc/>
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
            return false;

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId))
            return false;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsEnabled)
            return false;

        // Reject if the role embedded in the cookie no longer matches the DB.
        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(roleClaim, user.Role, StringComparison.Ordinal);
    }
}
