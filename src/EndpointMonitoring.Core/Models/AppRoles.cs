namespace EndpointMonitoring.Core.Models;

/// <summary>
/// Application roles. Single source of truth shared by seeding, OIDC claim mapping
/// and <c>[Authorize(Roles = ...)]</c> attributes.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";
}
