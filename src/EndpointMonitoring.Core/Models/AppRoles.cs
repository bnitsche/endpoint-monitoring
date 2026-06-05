namespace EndpointMonitoring.Core.Models;

/// <summary>
/// Application roles. Single source of truth shared by seeding, OIDC claim mapping
/// and <c>[Authorize(Roles = ...)]</c> attributes.
/// </summary>
public static class AppRoles
{
    /// <summary>Full administrative access.</summary>
    public const string Admin = "Admin";

    /// <summary>Read-only access to the dashboard and history.</summary>
    public const string Viewer = "Viewer";
}
