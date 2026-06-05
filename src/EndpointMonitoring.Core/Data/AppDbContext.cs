using EndpointMonitoring.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Core.Data;

/// <summary>EF Core database context for the endpoint monitoring application.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Monitored endpoint definitions.</summary>
    public DbSet<MonitoredEndpoint> Endpoints => Set<MonitoredEndpoint>();

    /// <summary>Historical check results.</summary>
    public DbSet<MonitoringResult> Results => Set<MonitoringResult>();

    /// <summary>Application users (local and external).</summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoredEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ProviderType).IsRequired().HasMaxLength(100);
            e.Property(x => x.ProviderConfig).IsRequired();
            e.Property(x => x.AlertSentAt).IsRequired(false);
            e.HasMany(x => x.Results)
             .WithOne(r => r.Endpoint)
             .HasForeignKey(r => r.EndpointId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonitoringResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EndpointId, x.CheckedAt });
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Role).IsRequired().HasMaxLength(50);
            e.Property(x => x.SendNotification).IsRequired().HasDefaultValue(false);
        });
    }
}
