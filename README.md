# Endpoint Monitoring

A self-hosted endpoint monitoring solution for Windows Server. Monitors HTTP/HTTPS endpoints, network hosts (ICMP), and Fritz!Box internet connectivity — with a Blazor web dashboard, email alerts, and flexible authentication.

## Features

- **Multiple monitoring providers** — HTTP/HTTPS status checks, ICMP ping, Fritz!Box UPnP/IGD
- **Configurable check intervals** — per-endpoint polling in seconds
- **Email alerts** — SMTP notifications on failure with deduplication (one alert per incident)
- **Alert acknowledgment** — clears automatically on endpoint recovery
- **Role-based access** — Admin and Viewer roles
- **Authentication** — local username/password accounts or OpenID Connect (e.g. Zitadel)
- **Blazor Server UI** — real-time dashboard built with MudBlazor
- **SQLite database** — single-file, zero-maintenance persistence

## Architecture

The solution is split into two independently deployed components that share a single SQLite database:

| Component | Type | Description |
|-----------|------|-------------|
| `EndpointMonitoring.Web` | ASP.NET Core / Blazor | Web dashboard, user management, configuration |
| `EndpointMonitoring.MonitoringService` | .NET Worker Service | Background check runner, email alert dispatch |

Both components write to the same SQLite database file. The monitoring service runs as a Windows Service; the web app runs under IIS via the ASP.NET Core Module.

## Monitoring Providers

### HTTP / HTTPS
Sends an HTTP request to the configured URL and verifies the response status code. Supports configurable timeouts and allows self-signed certificates.

### Ping (ICMP)
Sends ICMP echo requests to a host and reports reachability and round-trip time.

### Fritz!Box
Queries a Fritz!Box router via UPnP/IGD (TR-064) to monitor internet connectivity status and retrieve the current external IP address.

## Deployment

### Prerequisites

- Windows Server with IIS installed
- [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) (runtime + ASP.NET Core Module)
- A shared folder readable and writable by both the IIS App Pool identity and the Windows Service account (for the database file)

### Build & Publish

```powershell
dotnet publish src\EndpointMonitoring.Web -c Release -o publish\web
dotnet publish src\EndpointMonitoring.MonitoringService -c Release -o publish\service
```

### Web Application (IIS)

1. Copy `publish\web` to the target server (e.g. `C:\inetpub\wwwroot\endpoint-monitoring\`).
2. In IIS Manager, create a new website or application pointing to that folder.
3. Create a new Application Pool:
   - **.NET CLR version:** No Managed Code
   - **Identity:** an account with read/write access to the database folder
4. Configure the site binding (HTTP port 80 / HTTPS port 443).
5. For HTTPS, bind a certificate to port 443. HTTP requests are redirected to HTTPS automatically in production.

The `web.config` is generated automatically by `dotnet publish`.

### Monitoring Service (Windows Service)

1. Copy `publish\service` to the target server (e.g. `C:\Services\endpoint-monitoring\`).
2. Install and start the service:

```powershell
sc.exe create "EndpointMonitoringService" `
  binPath="C:\Services\endpoint-monitoring\EndpointMonitoring.MonitoringService.exe" `
  start=auto

sc.exe start EndpointMonitoringService
```

3. Ensure the service account has read/write access to the database folder.

### Database

Both components call `EnsureCreated()` on startup and apply any schema migrations automatically. No manual database setup is required. By default, the database is created at:

```
C:\ProgramData\EndpointMonitoring\monitoring.db
```

Override with the `DatabasePath` environment variable (set identically in both components).

## Configuration

### Web Application — `appsettings.json`

```json
{
  "Auth": {
    "DefaultAdmin": {
      "Username": "admin",
      "Password": "ChangeMe!123"
    }
  },
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "monitoring@example.com",
    "FromName": "Endpoint Monitoring"
  },
  "Oidc": {
    "Enabled": false,
    "Authority": "https://<instance>.zitadel.cloud",
    "ClientId": "",
    "ClientSecret": "",
    "Scopes": ["openid", "profile", "email"],
    "RolesClaim": "urn:zitadel:iam:org:project:roles",
    "AdminRoleValue": "admin",
    "ViewerRoleValue": "viewer"
  }
}
```

### Monitoring Service — `appsettings.json`

```json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "monitoring@example.com",
    "FromName": "Endpoint Monitoring"
  },
  "WebsiteUrl": "https://monitoring.example.com"
}
```

`WebsiteUrl` is included in alert emails as a link to the dashboard.

### Environment Variables

| Variable | Description |
|----------|-------------|
| `DatabasePath` | Absolute path to the SQLite database file. Must match in both components. |
| `WebsiteUrl` | Dashboard URL embedded in alert emails (MonitoringService only). |
| `ASPNETCORE_ENVIRONMENT` | Set to `Development` to disable HTTPS redirect and relax other production settings. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional OpenTelemetry OTLP collector endpoint (e.g. `http://collector:4317`). |

## Authentication

### Local Accounts (Default)

A default admin account is seeded on first startup using the credentials in `Auth:DefaultAdmin`. **Change the default password immediately after first login.**

Admins can create additional users, assign roles, reset passwords, and control per-user email notification opt-in from the Users page.

### OpenID Connect (Optional)

Set `Oidc:Enabled` to `true` and configure the provider details. Users are provisioned automatically on first login. Role mapping is based on a configurable JWT claim. Local accounts and OIDC accounts can coexist.

The fixed callback paths are `/signin-oidc` and `/signout-callback-oidc` — register these as redirect URIs in your identity provider.

## Email Alerts

When an endpoint check fails:

1. The monitoring service checks whether an alert has already been sent for this incident (`AlertSentAt` timestamp).
2. If not, it sends an email to all enabled users with `SendNotification = true`.
3. The alert is not re-sent on subsequent failures of the same incident.
4. When the endpoint recovers, `AlertSentAt` is cleared so the next failure triggers a fresh alert.

SMTP supports STARTTLS (port 587, default) and implicit SSL (port 465, set `UseSsl: true`). SMTP authentication is optional.

Use the **Send Test Email** button in the admin UI to verify SMTP connectivity before relying on alerts.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| Web framework | ASP.NET Core / Blazor Server |
| UI components | MudBlazor 9 |
| ORM | Entity Framework Core 10 |
| Database | SQLite (WAL mode) |
| Email | MailKit 4 |
| Auth | ASP.NET Core Cookie Auth + optional OIDC |
| Observability | OpenTelemetry (optional OTLP export) |

## Development

The solution includes a `.NET Aspire` AppHost project (`EndpointMonitoring.AppHost`) for local orchestration during development. Start it to run both the web app and monitoring service together with service discovery and structured logging.

```powershell
dotnet run --project src\EndpointMonitoring.AppHost
```

Set `ASPNETCORE_ENVIRONMENT=Development` to disable HTTPS redirects and use relaxed certificate validation.
