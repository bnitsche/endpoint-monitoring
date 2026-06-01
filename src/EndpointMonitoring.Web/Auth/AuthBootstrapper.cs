using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Web.Auth;

/// <summary>
/// Ensures the <c>Users</c> table exists and seeds a default administrator.
/// </summary>
/// <remarks>
/// The schema is created via <c>EnsureCreated()</c>, which does NOT add new tables to an
/// already-existing database. This idempotent raw-SQL bootstrap covers that case. The DDL
/// mirrors exactly what EF Core emits for <see cref="User"/> on SQLite — keep it in sync.
/// </remarks>
public static class AuthBootstrapper
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS "Users" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
            "Username" TEXT NOT NULL,
            "PasswordHash" TEXT NULL,
            "Role" TEXT NOT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "Email" TEXT NULL,
            "DisplayName" TEXT NULL,
            "IsExternal" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """;

    private const string CreateIndexSql =
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");""";

    public static async Task EnsureUsersTableAndSeedAsync(
        IDbContextFactory<AppDbContext> dbFactory,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger logger)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        await db.Database.ExecuteSqlRawAsync(CreateTableSql);
        await db.Database.ExecuteSqlRawAsync(CreateIndexSql);
        await EnsureNewColumnsAsync(db);

        if (await db.Users.AnyAsync())
            return;

        var username = configuration["Auth:DefaultAdmin:Username"] ?? "admin";
        var password = configuration["Auth:DefaultAdmin:Password"] ?? "ChangeMe!123";

        var admin = new User
        {
            Username = username,
            Role = AppRoles.Admin,
            IsEnabled = true,
            IsExternal = false,
            DisplayName = "Administrator",
            CreatedAt = DateTime.UtcNow,
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        logger.LogWarning(
            "Seeded default admin user '{Username}'. CHANGE THIS PASSWORD via the Users page " +
            "or the Auth:DefaultAdmin configuration (user-secrets / environment variables).",
            username);
    }

    private static async Task EnsureNewColumnsAsync(AppDbContext db)
    {
        // SQLite has no ALTER TABLE ... ADD COLUMN IF NOT EXISTS, so check PRAGMA first.
        var userCols = await db.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Users')")
            .ToListAsync();
        if (!userCols.Contains("SendNotification"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Users\" ADD COLUMN \"SendNotification\" INTEGER NOT NULL DEFAULT 0;");

        var epCols = await db.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Endpoints')")
            .ToListAsync();
        if (!epCols.Contains("AlertSentAt"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Endpoints\" ADD COLUMN \"AlertSentAt\" TEXT NULL;");
    }
}
