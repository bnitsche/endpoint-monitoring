# Endpoint Monitoring

A self-hosted endpoint monitoring solution for Windows Server. Monitors HTTP/HTTPS endpoints, network hosts (ICMP), and Fritz!Box internet connectivity — with a real-time Blazor web dashboard (SignalR push updates), email alerts, and flexible authentication.

## Features

- **Multiple monitoring providers** — HTTP/HTTPS status checks, ICMP ping, Fritz!Box UPnP/IGD
- **Configurable check intervals** — per-endpoint polling in seconds
- **Real-time dashboard (SignalR)** — the monitoring service pushes check results to the web app; dashboard rows update live (no manual refresh), changed rows flash briefly, and a chip shows the timestamp of the last received signal
- **Dashboard overview** — summary cards (Total / Healthy / Failing / No Data) plus a status table with last check, last error, response time, and status message per endpoint
- **Email alerts** — SMTP notifications on failure with deduplication (one alert per incident)
- **Alert acknowledgment** — failed endpoints show a *Recovered* state after coming back up until an admin acknowledges the alert from the dashboard; acknowledging re-arms notifications
- **Per-user notification opt-in** — admins control which users receive alert emails
- **Check history** — filterable result history (endpoint, OK/FAIL status, date range) with pagination and direct page jump
- **Role-based access** — Admin and Viewer roles
- **Authentication** — local username/password accounts or OpenID Connect (e.g. Zitadel)
- **Blazor Server UI** — responsive dashboard built with MudBlazor, localized date/time formatting based on the browser culture
- **SQLite database** — single-file, zero-maintenance persistence
- **Observability** — optional OpenTelemetry OTLP export

## Architecture

The solution is split into two independently deployed components that share a single SQLite database:

| Component | Type | Description |
|-----------|------|-------------|
| `EndpointMonitoring.Web` | ASP.NET Core / Blazor | Web dashboard, SignalR hub, user management, configuration |
| `EndpointMonitoring.MonitoringService` | .NET Worker Service | Background check runner, email alert dispatch, SignalR push client |

Both components write to the same SQLite database file. The monitoring service runs as a Windows Service; the web app runs under IIS via the ASP.NET Core Module.

### Real-Time Updates (SignalR)

In addition to the shared database, the monitoring service maintains a SignalR connection to the web app:

1. The web app hosts a SignalR hub at `/hubs/monitoring`.
2. After every endpoint check, the monitoring service invokes `NotifyCheckCompleted(endpointId)` on the hub.
3. The hub relays the event in-process to all connected Blazor circuits, which reload only the affected dashboard row.

The hub URL is resolved from Aspire service discovery during development, or from the `WebsiteUrl` setting in production. The client connects with automatic reconnect plus a 30-second retry loop for the initial connection — if the web app is unreachable, monitoring continues normally and real-time push simply resumes once the connection is re-established. The dashboard falls back to manual refresh at any time.

> **Note:** The hub endpoint allows anonymous access (the Windows Service has no user identity) and the client accepts self-signed certificates, so it works with internal/self-signed HTTPS setups out of the box.

## Monitoring Providers

### HTTP / HTTPS
Sends an HTTP request to the configured URL and verifies the response status code. Supports configurable timeouts and allows self-signed certificates.

### Ping (ICMP)
Sends ICMP echo requests to a host and reports reachability and round-trip time.

### Fritz!Box
Queries a Fritz!Box router via UPnP/IGD (TR-064) to monitor internet connectivity status and retrieve the current external IP address.

## Web UI

| Page | Access | Description |
|------|--------|-------------|
| **Dashboard** | All users | Summary cards (Total / Healthy / Failing / No Data) and live status table with last check, last error, response time, and message per endpoint. Updates in real time via SignalR — updated rows flash and a chip shows when the last signal was received. Admins acknowledge recovered alerts here. |
| **Endpoints** | Admin | Create, edit, delete, and enable/disable monitored endpoints with provider-specific configuration and per-endpoint check interval. |
| **History** | All users | Browse all check results, filterable by endpoint, OK/FAIL status, and date range. Paginated with configurable page size and direct page jump. |
| **Users** | Admin | Manage local and OIDC users: create, edit, enable/disable, delete, reset passwords, toggle email notification opt-in, and send a test email per user. |

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

Override the location with the `DatabasePath` configuration value — either as an environment variable or as an optional top-level entry in `appsettings.json` (set identically in both components):

```json
{
  "DatabasePath": "D:\\Data\\EndpointMonitoring\\monitoring.db"
}
```

Internally this resolves to the SQLite connection string `Data Source=<DatabasePath>;Mode=ReadWriteCreate;Cache=Shared` with WAL journaling enabled.

## Configuration

### Web Application — `appsettings.json`

```json
{
  "DatabasePath": "C:\\ProgramData\\EndpointMonitoring\\monitoring.db",
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
  "DatabasePath": "C:\\ProgramData\\EndpointMonitoring\\monitoring.db",
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

`DatabasePath` is optional — when omitted, the default path under `C:\ProgramData` is used (see [Database](#database)).

`WebsiteUrl` serves two purposes: it is included in alert emails as a link to the dashboard, and it is the base URL the monitoring service uses to reach the SignalR hub (`<WebsiteUrl>/hubs/monitoring`) for real-time dashboard updates. If it is missing, monitoring and email alerts still work — only real-time push is disabled.

### Environment Variables

All `appsettings.json` values can alternatively be supplied as environment variables (use `__` as the section separator, e.g. `Smtp__Host`). The most relevant ones:

| Variable | Description |
|----------|-------------|
| `DatabasePath` | Absolute path to the SQLite database file. Must match in both components. Optional — defaults to `C:\ProgramData\EndpointMonitoring\monitoring.db`. |
| `WebsiteUrl` | Dashboard URL — embedded in alert emails and used as the SignalR hub base URL (MonitoringService only). |
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
| Real-time push | SignalR (hub in Web, .NET client in MonitoringService) |
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

The AppHost propagates a shared `DatabasePath` to both projects and wires the monitoring service to the web app via `WithReference`, so the SignalR hub client finds the web app through service discovery — no `WebsiteUrl` needed during development. The monitoring service waits for the web app's health check before starting, ensuring the hub is reachable.

Set `ASPNETCORE_ENVIRONMENT=Development` to disable HTTPS redirects and use relaxed certificate validation.
