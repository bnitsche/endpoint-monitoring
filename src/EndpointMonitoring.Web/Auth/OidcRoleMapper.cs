using System.Security.Claims;
using System.Text.Json;
using EndpointMonitoring.Core.Models;

namespace EndpointMonitoring.Web.Auth;

/// <summary>
/// Maps the role claim emitted by the OIDC provider to an application role.
/// </summary>
public static class OidcRoleMapper
{
    /// <summary>
    /// Resolves the app role from the configured roles claim. Returns <see cref="AppRoles.Admin"/>
    /// if the admin role value is present, otherwise <see cref="AppRoles.Viewer"/>.
    /// </summary>
    public static string ResolveRole(ClaimsPrincipal principal, OidcOptions options)
    {
        var values = ExtractRoleValues(principal, options.RolesClaim);

        if (values.Contains(options.AdminRoleValue, StringComparer.OrdinalIgnoreCase))
            return AppRoles.Admin;

        return AppRoles.Viewer;
    }

    /// <summary>
    /// Extracts role values from the claim. Zitadel emits the project-roles claim as a JSON
    /// object keyed by role name; other providers may use a JSON array or repeated string
    /// claims. All three shapes are supported.
    /// </summary>
    private static HashSet<string> ExtractRoleValues(ClaimsPrincipal principal, string rolesClaim)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.FindAll(rolesClaim))
        {
            var value = claim.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                try
                {
                    using var doc = JsonDocument.Parse(value);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in root.EnumerateObject())
                            result.Add(prop.Name);
                        continue;
                    }
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                            if (item.ValueKind == JsonValueKind.String)
                                result.Add(item.GetString()!);
                        continue;
                    }
                }
                catch (JsonException)
                {
                    // Fall through and treat the raw value as a single role.
                }
            }

            result.Add(value);
        }

        return result;
    }
}
