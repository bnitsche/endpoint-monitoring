var builder = DistributedApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EndpointMonitoring", "monitoring.db");

var monitoringService = builder.AddProject<Projects.EndpointMonitoring_MonitoringService>("monitoring-service")
    .WithEnvironment("DatabasePath", dbPath);

builder.AddProject<Projects.EndpointMonitoring_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithEnvironment("DatabasePath", dbPath)
    .WithReference(monitoringService)
    .WaitFor(monitoringService);

builder.Build().Run();
