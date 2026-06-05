using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EndpointMonitoring.Core;

/// <summary>DI registration helpers for the Core library.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the EF Core context factory and provider registry.</summary>
    public static IServiceCollection AddEndpointMonitoringCore(
        this IServiceCollection services,
        string dbPath)
    {
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared");
        });

        services.AddSingleton<MonitoringProviderRegistry>();

        return services;
    }

    /// <summary>Registers <typeparamref name="TProvider"/> as an <see cref="IMonitoringProvider"/> singleton.</summary>
    public static IServiceCollection AddMonitoringProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IMonitoringProvider
    {
        services.AddSingleton<IMonitoringProvider, TProvider>();
        return services;
    }
}
