# PicoBusX

🚌 A minimalist **Azure Service Bus Explorer** built with ASP.NET Core Blazor Server (.NET 10).

Built with **Aspire 13.1.2** for local development orchestration and **Microsoft FluentUI Blazor** for a dashboard UI inspired by the .NET Aspire Dashboard.

![PicoBusX Dashboard](https://github.com/user-attachments/assets/cc13c01d-4860-4701-b6db-1b59db5968d3)

## Screenshots

### Runtime Connection Setup

![PicoBusX Connection Setup - Unconfigured State](https://github.com/user-attachments/assets/71bf20c1-578d-4e6a-bd61-8ad126052075)

![PicoBusX Connection Setup - Connection String](https://github.com/user-attachments/assets/d9d40979-578e-4f84-bcb8-3889029d35b3)

![PicoBusX Connection Setup - Service Principal](https://github.com/user-attachments/assets/ff59249b-f69b-4d6f-957c-7276b2dabb29)

---

## Features

- 🌲 **Interactive TreeView** — lists Queues, Topics, and Subscriptions (with filter/search)
- 📋 **Entity Details** — active message count, dead-letter count, lock duration, session info, timestamps
- 📤 **Send Message** — JSON editor with Format / Minify / Validate, optional headers, application properties
- 👁️ **Peek / Read Messages** — non-destructive peek or PeekLock receive, with expandable message cards (body pretty-printed if JSON); session-enabled entities iterate across sessions automatically
- ☠️ **Dead-Letter Queue Browser** — dedicated DLQ panel per entity with Peek and Resubmit support
- 🔍 **Message Filtering** — client-side filter across MessageId, Subject, CorrelationId, SessionId, Body, and ApplicationProperties
- 📄 **Load More / Pagination** — sequence-number-based continuation loads additional messages beyond the initial batch in both Peek and DLQ panels
- 🔐 **Multiple Auth Modes** — connection string (SAS), DefaultAzureCredential, or Service Principal (Entra ID)
- ✅ **Connection Status** — banner showing connected/not-connected with error details

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- An Azure Service Bus namespace with a connection string (Standard or Premium tier)
- (Optional) [Docker Desktop](https://www.docker.com/products/docker-desktop) for running with Aspire

---

## How to Run

### Option 1: Using Aspire Host (Recommended for Development)

Aspire automatically orchestrates PicoBusX with a local Azure Service Bus Emulator.

**Prerequisites:**
- Install [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/setup-tooling?tabs=windows):
  ```bash
  dotnet workload install aspire
  ```
- Docker Desktop must be running

```bash
cd src/PicoBusX.AppHost
dotnet run
```

The **Aspire Dashboard** opens at `http://localhost:4317`, showing the PicoBusX web app, the Azure Service Bus Emulator, traces, and logs.

Then visit: **http://localhost:5000** (or the endpoint shown in the dashboard).

See [PicoBusX.AppHost README](./src/PicoBusX.AppHost/README.md) for more details.

### Option 2: Manual Setup (Standalone)

```bash
# Clone the repo
git clone https://github.com/evilz/PicoBusX.git
cd PicoBusX

# Set your connection string via user secrets
dotnet user-secrets set "ServiceBus:SERVICEBUS_CONNECTIONSTRING" "<your-connection-string>" \
  --project src/PicoBusX.Web/PicoBusX.Web.csproj

# Start the app
dotnet run --project src/PicoBusX.Web/PicoBusX.Web.csproj
```

Then open [https://localhost:7270](https://localhost:7270).

### Run tests

```bash
dotnet test tests/PicoBusX.Web.Tests/PicoBusX.Web.Tests.csproj
```

---

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ServiceBus__AuthType` | No | `ConnectionString` | Auth mode: `ConnectionString`, `DefaultAzureCredential`, or `ServicePrincipal` |
| `ServiceBus__SERVICEBUS_CONNECTIONSTRING` | When `AuthType=ConnectionString` | — | Azure Service Bus connection string (SAS) |
| `ServiceBus__SERVICEBUS_FULLYQUALIFIEDNAMESPACE` | When using AD auth | — | Fully-qualified namespace, e.g. `<ns>.servicebus.windows.net` |
| `ServiceBus__TenantId` | When `AuthType=ServicePrincipal` | — | Entra ID tenant ID |
| `ServiceBus__ClientId` | When `AuthType=ServicePrincipal` | — | Service principal application (client) ID |
| `ServiceBus__ClientSecret` | When `AuthType=ServicePrincipal` | — | Service principal client secret |
| `ServiceBus__TransportType` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |
| `ServiceBus__EntityMaxPeek` | No | `10` | Default max messages for Peek/Receive |

### Alternative: appsettings.json

```json
{
  "ServiceBus": {
    "AuthType": "ConnectionString",
    "SERVICEBUS_CONNECTIONSTRING": "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;...",
    "TransportType": "AmqpTcp",
    "EntityMaxPeek": 10
  }
}
```

> **⚠️ Never commit credentials to source control.** Prefer environment variables or user secrets for local development.

---

## CI/CD Pipeline

The project uses GitHub Actions to **build**, **test**, and **publish a Docker image** to GitHub Container Registry (`ghcr.io`) on every push to `main`.

### CI/CD assets in this repository

The following files are already committed and provide the full CI/CD and containerization setup:

- [GitHub Actions workflow](.github/workflows/ci.yml) — two-job pipeline: build & test, then Docker build & push
- [Dockerfile](Dockerfile) — multi-stage build: `sdk:10.0` → `aspnet:10.0`, port 8080
- [.dockerignore](.dockerignore) — excludes build artifacts, tests, and VCS metadata

The workflow publishes to `ghcr.io/<owner>/picobusx` with tags: `latest` (main branch), branch name, and `sha-<commit>`.

### Run the Docker image

```bash
docker run \
  -e ServiceBus__SERVICEBUS_CONNECTIONSTRING="<your-connection-string>" \
  -p 8080:8080 \
  ghcr.io/evilz/picobusx:latest
```

---

## Project Structure

```
src/
├── PicoBusX.AppHost/          # Aspire Host (.NET 10)
│   ├── AppHost.cs             # Aspire orchestration configuration
│   ├── PicoBusX.AppHost.csproj
│   └── README.md              # Detailed Aspire documentation
│
└── PicoBusX.Web/              # Blazor Server (.NET 10)
    ├── Components/
    │   ├── Pages/
    │   │   ├── Home.razor             # Main dashboard (tree + details + send + peek)
    │   │   └── Settings.razor         # Connection settings page
    │   ├── Layout/
    │   │   └── MainLayout.razor       # Minimal dark-header layout
    │   ├── BusTreeView.razor          # Collapsible tree with search
    │   ├── EntityDetailsPanel.razor   # Queue/Topic/Subscription property tables
    │   ├── JsonMessageEditor.razor    # JSON textarea editor (format/minify/validate)
    │   ├── MessagePanelBase.cs        # Shared abstract base for message panels
    │   ├── PeekReadPanel.razor        # Peek / Receive message browser
    │   ├── DlqPanel.razor             # Dead-letter queue browser
    │   └── MonacoEditorOptions.cs     # Shared Monaco editor configuration
    ├── Models/                        # QueueInfo, TopicInfo, BrowsedMessage, etc.
    ├── Options/
    │   └── ServiceBusConnectionOptions.cs
    ├── Services/
    │   ├── ServiceBusClientFactory.cs # Singleton client/admin client factory
    │   ├── ExplorerService.cs         # List entities + runtime properties
    │   ├── MessageSenderService.cs    # Send JSON messages
    │   └── MessageBrowserService.cs   # Peek / Receive messages (with pagination)
    ├── Program.cs
    └── appsettings.json
```

---

## Known Limits

- **Azure Service Bus Emulator** — ✅ Supported when running under Aspire
- **Peek is non-destructive** — uses `PeekMessages`; Receive uses PeekLock and abandons immediately
- **No reconnect / retry UI** — restart the app if the connection string changes
- **Sessions + Load More** — pagination is not available for session-enabled entities

---

## Contributing

Contributions are welcome! Open an issue or submit a pull request.

## License

MIT
