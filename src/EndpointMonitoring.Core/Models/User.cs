namespace EndpointMonitoring.Core.Models;

/// <summary>An application user (local or federated via OIDC).</summary>
public class User
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Login name (local accounts) or the OIDC <c>sub</c> for external users.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by PasswordHasher. Null for external (OIDC-only) accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>One of <see cref="AppRoles"/>.</summary>
    public string Role { get; set; } = AppRoles.Viewer;

    /// <summary>When false, the user cannot log in.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Optional email address for notifications.</summary>
    public string? Email { get; set; }

    /// <summary>Optional display name shown in the UI.</summary>
    public string? DisplayName { get; set; }

    /// <summary>True when the account was provisioned via an external OIDC provider.</summary>
    public bool IsExternal { get; set; }

    /// <summary>When true, this user receives failure alert emails.</summary>
    public bool SendNotification { get; set; }

    /// <summary>UTC timestamp when this user was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
