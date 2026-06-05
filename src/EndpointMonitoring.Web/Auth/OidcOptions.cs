namespace EndpointMonitoring.Web.Auth;

/// <summary>
/// Configuration for the optional external OpenID Connect provider, bound from the
/// <c>Oidc</c> configuration section. Provider-agnostic; Zitadel is the documented example.
/// </summary>
public sealed class OidcOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Oidc";

    /// <summary>When <see langword="false"/>, OIDC login is hidden and the OIDC middleware is not registered.</summary>
    public bool Enabled { get; set; }

    /// <summary>Issuer URL of the OpenID Connect provider.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client ID registered with the provider.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Scopes requested during the authorization flow.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>
    /// Name of the claim carrying the user's role(s). For Zitadel this is the project-roles
    /// claim, whose value is a JSON object keyed by role name.
    /// </summary>
    public string RolesClaim { get; set; } = "urn:zitadel:iam:org:project:roles";

    /// <summary>Value within <see cref="RolesClaim"/> that maps to the Admin role.</summary>
    public string AdminRoleValue { get; set; } = "admin";

    /// <summary>Value within <see cref="RolesClaim"/> that maps to the Viewer role.</summary>
    public string ViewerRoleValue { get; set; } = "viewer";
}
