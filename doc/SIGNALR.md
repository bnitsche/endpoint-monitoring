# SignalR Real-Time Updates — Architecture & Workflow

This document explains how real-time dashboard updates work in Endpoint Monitoring:
why the SignalR **hub** lives in the **Web** project and the SignalR **client** in the
**MonitoringService**, and how an endpoint check travels all the way to the browser.

## TL;DR

> The MonitoringService is a headless Windows Service worker with **no HTTP server**,
> so it *cannot* host a SignalR hub. The Web project already runs Kestrel, so it hosts
> the hub and the worker connects to it as an outbound client. Data still flows
> service → web, because SignalR clients can invoke hub methods.

## Components

| Component | Project | File | Role |
|---|---|---|---|
| `MonitoringHub` | EndpointMonitoring.Web | `Hubs/MonitoringHub.cs` | SignalR **hub** (server side). Receives `NotifyCheckCompleted(endpointId)` calls from the worker. |
| `IMonitoringUpdateNotifier` / `MonitoringUpdateNotifier` | EndpointMonitoring.Web | `Services/IMonitoringUpdateNotifier.cs` | In-process singleton event bus. Bridges the hub to Blazor components via a plain C# event. |
| `MonitoringHubClient` | EndpointMonitoring.MonitoringService | `SignalR/MonitoringHubClient.cs` | SignalR **client** (hosted service). Maintains the connection to the web hub and pushes check-completed events. |
| `EndpointMonitoringWorker` | EndpointMonitoring.MonitoringService | `EndpointMonitoringWorker.cs` | After each check is persisted, calls `MonitoringHubClient.NotifyCheckCompletedAsync(id)`. |
| `Home.razor` (Dashboard) | EndpointMonitoring.Web | `Components/Pages/Home.razor` | Subscribes to `OnEndpointChecked`, refreshes the affected row, updates the "last signal" indicator. |

## End-to-end workflow

```
MonitoringService (Windows Service, no HTTP listener)
┌─────────────────────────────────────────────────────┐
│ EndpointMonitoringWorker                            │
│   1. runs check, saves result to SQLite             │
│   2. await _hubClient.NotifyCheckCompletedAsync(id) │
│                                                     │
│ MonitoringHubClient (SignalR *client*)              │
│   3. InvokeAsync("NotifyCheckCompleted", id) ───────┼──┐ outbound
└─────────────────────────────────────────────────────┘  │ HTTPS/WebSocket
                                                         ▼
Web project (ASP.NET Core / Blazor Server)               │
┌─────────────────────────────────────────────────────┐  │
│ MonitoringHub (SignalR *hub*, /hubs/monitoring) ◄───┼──┘
│   4. _notifier.NotifyEndpointChecked(id)            │
│                                                     │
│ MonitoringUpdateNotifier (singleton event bus)      │
│   5. raises OnEndpointChecked(id)                   │
│                                                     │
│ Home.razor (per-circuit subscriber)                 │
│   6. InvokeAsync → RefreshRowAsync(id)              │
│      + update "last signal received" timestamp      │
└──────────────────────┬──────────────────────────────┘
                       │ Blazor Server's *built-in*
                       ▼ SignalR circuit (automatic)
                   Browser re-renders the dashboard row
```

Steps in detail:

1. **Check executes** — `EndpointMonitoringWorker` runs a provider check (HTTP / Ping /
   Fritz!Box) and writes the result (and any alert state) to the shared SQLite database.
2. **Push notification** — the worker calls `NotifyCheckCompletedAsync(endpoint.Id)`
   (`EndpointMonitoringWorker.cs:151`). This is fire-and-tolerate: if the hub is not
   connected, the method silently no-ops — monitoring never depends on the web app.
3. **Hub invocation** — `MonitoringHubClient` invokes the hub method
   `NotifyCheckCompleted` over the persistent connection to `/hubs/monitoring`.
4. **Hub → event bus** — `MonitoringHub` doesn't broadcast to SignalR clients; it simply
   forwards the ID to the in-process `IMonitoringUpdateNotifier` singleton.
5. **Event fan-out** — every open Blazor circuit (i.e. every connected dashboard user)
   that subscribed to `OnEndpointChecked` gets the event.
6. **UI refresh** — `Home.razor` reloads just the affected endpoint row from the
   database (`RefreshRowAsync`) and re-renders. The browser receives the diff through
   **Blazor Server's own built-in SignalR circuit** — no custom JavaScript client exists.

> Note there are **two distinct SignalR usages**: the custom service→web hub described
> here, and Blazor Server's internal web→browser circuit, which the framework manages
> automatically. The browser never connects to `/hubs/monitoring`.

## Connection management (service side)

`MonitoringHubClient` is registered as a singleton **and** a hosted service
(`Program.cs:35-36`), so the worker and the connection share one instance.

- **URL resolution** — under Aspire, `WithReference(web)` injects the frontend URL as
  `services:webfrontend:https:0`; standalone deployments fall back to the `WebsiteUrl`
  config key. If neither is set, real-time push is disabled with a warning and the
  service runs normally otherwise.
- **Initial connect & resilience** — a background retry loop attempts to connect every
  30 s until it succeeds (the web app may start later than the service).
  `WithAutomaticReconnect()` handles drops after a successful connection; if it gives
  up, the retry loop takes over again.
- **Certificate validation is disabled** for the hub connection
  (`DangerousAcceptAnyServerCertificateValidator`) to support self-signed dev/Aspire
  certificates.
- **Failure isolation** — every push is wrapped in try/catch; a broken web app can
  never break monitoring or alerting.

## Why not the other way around (hub in the service)?

Hosting the hub in the MonitoringService and having the web app connect as a client is
a legitimate alternative pattern, but for this solution it loses on every axis:

| Concern | Hub in Web (current) | Hub in MonitoringService |
|---|---|---|
| HTTP server | Already there (Kestrel) | Would require converting the worker into an ASP.NET Core app |
| Network exposure | Service makes **outbound** calls only | Service must **listen** on a port → firewall rule, TLS cert on the service |
| Windows Service fit | Clean headless worker | Worker + web host hybrid |
| Who needs whose URL | Service needs web URL (Aspire injects it) | Web needs service URL |
| Failure mode | Web down → pushes no-op, monitoring unaffected | Same, but with more moving parts |

The one scenario where the reverse design wins is **multiple web frontend replicas**:
each replica could subscribe to the service's hub independently, whereas today the
worker pushes to a single frontend URL. With one frontend instance (the deployment
model of this app), the current design is simpler and operationally safer.

## Known limitations / notes

- **`/hubs/monitoring` allows anonymous access** (`Program.cs:198`). The payload is
  just an endpoint ID and the hub only triggers a DB re-read, so the blast radius is
  low, but on an untrusted network anyone who can reach the web app could trigger
  spurious dashboard refreshes. A shared secret or client-certificate check would
  harden this.
- **Single-frontend assumption** — scaling the web app to multiple instances would
  require either the reverse topology or a SignalR backplane.
- **Polling fallback** — if the hub connection is down, the dashboard simply shows
  stale data until the next manual/auto refresh; the database remains the source of
  truth at all times.
