using EndpointMonitoring.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MonitoredEndpoint> Endpoints => Set<MonitoredEndpoint>();
    public DbSet<MonitoringResult> Results => Set<MonitoringResult>();
    public DbSet<User> Users => Set<User>();

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
