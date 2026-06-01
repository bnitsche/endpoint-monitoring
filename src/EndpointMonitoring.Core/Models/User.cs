namespace EndpointMonitoring.Core.Models;

public class User
{
    public int Id { get; set; }

    /// <summary>Login name (local accounts) or the OIDC <c>sub</c> for external users.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by PasswordHasher. Null for external (OIDC-only) accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>One of <see cref="AppRoles"/>.</summary>
    public string Role { get; set; } = AppRoles.Viewer;

    public bool IsEnabled { get; set; } = true;

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>True when the account was provisioned via an external OIDC provider.</summary>
    public bool IsExternal { get; set; }

    /// <summary>When true, this user receives failure alert emails.</summary>
    public bool SendNotification { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
