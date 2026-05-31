using EndpointMonitoring.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EndpointMonitoring.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MonitoredEndpoint> Endpoints => Set<MonitoredEndpoint>();
    public DbSet<MonitoringResult> Results => Set<MonitoringResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoredEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ProviderType).IsRequired().HasMaxLength(100);
            e.Property(x => x.ProviderConfig).IsRequired();
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
    }
}
