using EndpointMonitoring.Core;
using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Providers.FritzBox;
using EndpointMonitoring.Core.Providers.Http;
using EndpointMonitoring.Core.Providers.Ping;
using EndpointMonitoring.MonitoringService;
using EndpointMonitoring.Core.Notifications;
using EndpointMonitoring.MonitoringService.Notifications;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Endpoint Monitoring Service");

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

var smtpSettings = builder.Configuration
    .GetSection(SmtpSettings.SectionName)
    .Get<SmtpSettings>() ?? new SmtpSettings();
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddSingleton<EmailNotificationService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var ctx = factory.CreateDbContext();
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

    // Add new columns to existing databases — EnsureCreated won't add them.
    // SQLite has no IF NOT EXISTS for ALTER TABLE, so check PRAGMA first.
    var userCols = ctx.Database
        .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Users')")
        .ToList();
    if (!userCols.Contains("SendNotification"))
        ctx.Database.ExecuteSqlRaw(
            "ALTER TABLE \"Users\" ADD COLUMN \"SendNotification\" INTEGER NOT NULL DEFAULT 0;");

    var epCols = ctx.Database
        .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Endpoints')")
        .ToList();
    if (!epCols.Contains("AlertSentAt"))
        ctx.Database.ExecuteSqlRaw(
            "ALTER TABLE \"Endpoints\" ADD COLUMN \"AlertSentAt\" TEXT NULL;");
}

host.Run();
