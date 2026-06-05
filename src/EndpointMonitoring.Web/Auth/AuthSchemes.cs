namespace EndpointMonitoring.Web.Auth;

/// <summary>Authentication scheme name constants used across the application.</summary>
public static class AuthSchemes
{
    /// <summary>Cookie-based authentication scheme (primary).</summary>
    public const string Cookie = "Cookies";

    /// <summary>OpenID Connect scheme used for external identity providers.</summary>
    public const string Oidc = "oidc";
}
