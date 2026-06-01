using System.Security.Claims;
using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Web.Auth;

/// <summary>
/// Thrown when an operation would remove the last enabled administrator.
/// Surfaced to the admin UI (e.g. via Snackbar).
/// </summary>
public sealed class AuthOperationException(string message) : Exception(message);

/// <summary>
/// Single place for all authentication/user DB access. Shared by the login endpoint,
/// the OIDC sign-in callback and the admin user-management page.
/// </summary>
public sealed class UserAuthService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPasswordHasher<User> passwordHasher)
{
    /// <summary>
    /// Validates local credentials. Returns the user on success, or null when the user is
    /// unknown, disabled, external-only, or the password does not match.
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user is null || !user.IsEnabled || string.IsNullOrEmpty(user.PasswordHash))
            return null;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>Builds the claims principal persisted into the auth cookie.</summary>
    public static ClaimsPrincipal BuildPrincipal(User user, string authenticationScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName ?? user.Username),
            new(ClaimTypes.Role, user.Role),
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        var identity = new ClaimsIdentity(claims, authenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    // ---- Admin user management ------------------------------------------------------------

    public async Task<List<User>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<User?> FindAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.FindAsync(id);
    }

    public async Task CreateAsync(string username, string password, string role, string? email, string? displayName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Username == username))
            throw new AuthOperationException($"A user named '{username}' already exists.");

        var user = new User
        {
            Username = username,
            Role = role,
            Email = email,
            DisplayName = displayName,
            IsEnabled = true,
            IsExternal = false,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, string role, string? email, string? displayName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id)
            ?? throw new AuthOperationException("User not found.");

        if (user.Role == AppRoles.Admin && role != AppRoles.Admin)
            await GuardLastAdminAsync(db, user.Id);

        user.Role = role;
        user.Email = email;
        user.DisplayName = displayName;
        await db.SaveChangesAsync();
    }

    public async Task SetEnabledAsync(int id, bool enabled)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id)
            ?? throw new AuthOperationException("User not found.");

        if (!enabled && user.Role == AppRoles.Admin)
            await GuardLastAdminAsync(db, user.Id);

        user.IsEnabled = enabled;
        await db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(int id, string newPassword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id)
            ?? throw new AuthOperationException("User not found.");

        if (user.IsExternal)
            throw new AuthOperationException("External (SSO) users have no local password.");

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id)
            ?? throw new AuthOperationException("User not found.");

        if (user.Role == AppRoles.Admin)
            await GuardLastAdminAsync(db, user.Id);

        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Provisions or refreshes an external (OIDC) user. The role from the identity provider
    /// wins on every sign-in.
    /// </summary>
    public async Task<User> EnsureExternalUserAsync(string subject, string? email, string? displayName, string role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == subject);

        if (user is null)
        {
            user = new User
            {
                Username = subject,
                Role = role,
                Email = email,
                DisplayName = displayName,
                IsEnabled = true,
                IsExternal = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            user.Role = role;
            user.Email = email;
            user.DisplayName = displayName;
            user.IsExternal = true;
        }

        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Throws if <paramref name="excludingUserId"/> is the only enabled admin.</summary>
    private static async Task GuardLastAdminAsync(AppDbContext db, int excludingUserId)
    {
        var otherAdmins = await db.Users.CountAsync(u =>
            u.Role == AppRoles.Admin && u.IsEnabled && u.Id != excludingUserId);

        if (otherAdmins == 0)
            throw new AuthOperationException("Cannot remove the last enabled administrator.");
    }
}
