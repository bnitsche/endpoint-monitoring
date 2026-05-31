using EndpointMonitoring.Core;
using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Providers.FritzBox;
using EndpointMonitoring.Core.Providers.Http;
using EndpointMonitoring.Core.Providers.Ping;
using EndpointMonitoring.MonitoringService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EndpointMonitoring", "monitoring.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddEndpointMonitoringCore(dbPath);
builder.Services.AddMonitoringProvider<HttpMonitoringProvider>();
builder.Services.AddMonitoringProvider<PingMonitoringProvider>();
builder.Services.AddMonitoringProvider<FritzBoxMonitoringProvider>();

builder.Services.AddHttpClient("monitoring")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddHostedService<EndpointMonitoringWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var ctx = factory.CreateDbContext();
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

host.Run();
