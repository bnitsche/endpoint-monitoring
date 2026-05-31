using EndpointMonitoring.Core;
using EndpointMonitoring.Core.Data;
using EndpointMonitoring.Core.Providers.FritzBox;
using EndpointMonitoring.Core.Providers.Http;
using EndpointMonitoring.Core.Providers.Ping;
using EndpointMonitoring.Web.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options =>
    {
        // Keep disconnected circuits alive for 5 minutes so brief debugger
        // pauses or network hiccups don't lose the Blazor circuit.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    });

builder.Services.AddSignalR(options =>
{
    // Give the client more time to respond before the server drops the connection.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var ctx = factory.CreateDbContext();
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
