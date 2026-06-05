var builder = DistributedApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EndpointMonitoring", "monitoring.db");

var web = builder.AddProject<Projects.EndpointMonitoring_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithEnvironment("DatabasePath", dbPath);

builder.AddProject<Projects.EndpointMonitoring_MonitoringService>("monitoring-service")
    .WithEnvironment("DatabasePath", dbPath)
    .WithReference(web)       // injects service-discovery env vars so MonitoringHubClient finds the web app
    .WaitFor(web);            // ensures the hub endpoint is ready before the monitoring service starts

builder.Build().Run();
