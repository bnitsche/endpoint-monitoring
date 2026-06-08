using Microsoft.AspNetCore.Http;

namespace EndpointMonitoring.Web.Services;

/// <summary>
/// Resolves the originating client IP from an <see cref="HttpContext"/>, honouring a reverse-proxy
/// <c>X-Forwarded-For</c> header when present and falling back to the transport remote address.
/// </summary>
public static class ClientIp
{
    /// <summary>Returns the client IP, or <c>"unknown"</c> when it cannot be determined.</summary>
    public static string Resolve(HttpContext? context)
    {
        if (context is null)
            return "unknown";

        // Behind a reverse proxy the real client is the left-most entry of X-Forwarded-For.
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
