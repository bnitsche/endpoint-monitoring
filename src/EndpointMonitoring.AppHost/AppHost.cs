var builder = DistributedApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EndpointMonitoring", "monitoring.db");

var web = builder.AddProject<Projects.EndpointMonitoring_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    // Bind the launchSettings ports (7097/5290) directly instead of running behind DCP's proxy.
    // The proxy assigns the app a dynamic ephemeral port, which on Windows can land in a
    // Hyper-V/WinNAT reserved range and silently fail to bind (dotnet/aspire#9634).
    .WithEndpoint("https", e => e.IsProxied = false)
    .WithEndpoint("http", e => e.IsProxied = false)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithEnvironment("DatabasePath", dbPath);

builder.AddProject<Projects.EndpointMonitoring_MonitoringService>("monitoring-service")
    .WithEnvironment("DatabasePath", dbPath)
    .WithReference(web)       // injects service-discovery env vars so MonitoringHubClient finds the web app
    .WaitFor(web);            // ensures the hub endpoint is ready before the monitoring service starts

builder.Build().Run();
