using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EndpointMonitoring.Core;

public static class ServiceCollectionExtensions
{
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

    public static IServiceCollection AddMonitoringProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IMonitoringProvider
    {
        services.AddSingleton<IMonitoringProvider, TProvider>();
        return services;
    }
}
