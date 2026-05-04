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
- 👁️ **Peek / Read Messages** — non-destructive peek or PeekLock receive, with expandable message cards (body pretty-printed if JSON)
- ☠️ **Dead-Letter Queue Browser** — peek DLQ messages with dead-letter reason and error description; resubmit messages back to the main entity
- 🏗️ **Entity Management** — create and delete queues, topics, and subscriptions from the UI
- ⚙️ **Runtime Connection Settings** — configure the Azure Service Bus connection at `/settings` without restarting the app; settings are persisted to disk
- 🔑 **Multiple Authentication Modes** — Connection String (SAS), Managed Identity / Default Azure Credential, or Service Principal (client secret)
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

Authentication mode is selected with `ServiceBus__AuthType`. The default is `ConnectionString`.

### Connection String (SAS)

| Variable | Required | Default | Description |
|---|---|---|---|
| `ServiceBus__AuthType` | No | `ConnectionString` | Set to `ConnectionString` |
| `ServiceBus__SERVICEBUS_CONNECTIONSTRING` | ✅ Yes | — | Azure Service Bus SAS connection string |
| `ServiceBus__SERVICEBUS_ADMINCONNECTIONSTRING` | No | — | Separate connection string for management operations (list/create/delete entities). Falls back to the main connection string if omitted. |
| `ServiceBus__TransportType` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |
| `ServiceBus__EntityMaxPeek` | No | `10` | Default max messages for Peek/Receive |

### Managed Identity / Default Azure Credential

| Variable | Required | Default | Description |
|---|---|---|---|
| `ServiceBus__AuthType` | ✅ Yes | — | Set to `DefaultAzureCredential` |
| `ServiceBus__SERVICEBUS_FULLYQUALIFIEDNAMESPACE` | ✅ Yes | — | Namespace hostname, e.g. `myns.servicebus.windows.net` |
| `ServiceBus__TransportType` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |

### Service Principal (Client Secret)

| Variable | Required | Default | Description |
|---|---|---|---|
| `ServiceBus__AuthType` | ✅ Yes | — | Set to `ServicePrincipal` |
| `ServiceBus__SERVICEBUS_FULLYQUALIFIEDNAMESPACE` | ✅ Yes | — | Namespace hostname, e.g. `myns.servicebus.windows.net` |
| `ServiceBus__TenantId` | ✅ Yes | — | Microsoft Entra tenant ID |
| `ServiceBus__ClientId` | ✅ Yes | — | Application (client) ID |
| `ServiceBus__ClientSecret` | ✅ Yes | — | Client secret value |
| `ServiceBus__TransportType` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |

> **⚠️ Never commit credentials to source control.** Prefer environment variables or user secrets for local development. You can also configure the connection at runtime via the **Settings** page (`/settings`) — no restart required.

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
    │   │   ├── Home.razor             # Main dashboard (tree + details + send + peek + DLQ)
    │   │   └── Settings.razor         # Runtime connection settings
    │   ├── Layout/
    │   │   └── MainLayout.razor       # Minimal dark-header layout
    │   ├── BusTreeView.razor          # Collapsible tree with search
    │   ├── EntityDetailsPanel.razor   # Queue/Topic/Subscription property tables
    │   ├── JsonMessageEditor.razor    # JSON textarea editor (format/minify/validate)
    │   ├── MessagePanelToolbar.razor  # Shared toolbar (peek button, filter, load-more)
    │   ├── PeekReadPanel.razor        # Peek / Receive message browser
    │   ├── DlqPanel.razor             # Dead-letter queue browser with resubmit
    │   ├── MessageList.razor          # Reusable message list with badges and slots
    │   ├── MessageCard.razor          # Expandable single-message card
    │   └── LoadMoreButton.razor       # Paginated load-more control
    ├── Models/                        # QueueInfo, TopicInfo, SubscriptionInfo, BrowsedMessage, IQueueLikeEntity, etc.
    ├── Options/
    │   └── ServiceBusConnectionOptions.cs
    ├── Services/
    │   ├── ServiceBusClientFactory.cs # Singleton client/admin client factory with runtime reset
    │   ├── ExplorerService.cs         # List entities + runtime properties
    │   ├── MessageSenderService.cs    # Send JSON messages
    │   ├── MessageBrowserService.cs   # Peek / Receive / DLQ peek and resubmit
    │   ├── EntityManagementService.cs # Create and delete queues, topics, subscriptions
    │   └── ConnectionSettingsStore.cs # Persist runtime connection settings to disk
    ├── Program.cs
    └── appsettings.json
```

---

## Known Limits

- **Azure Service Bus Emulator** — ✅ Supported when running under Aspire
- **Peek is non-destructive** — uses `PeekMessages`; Receive uses PeekLock and abandons immediately
- **No message filtering** — peek returns next N messages from the head of the queue/subscription
- **No reconnect / retry UI** — use the Settings page (`/settings`) to update the connection if it changes; the connection is reset automatically on save
- **Sessions** — session-enabled queues/subscriptions are browsed via session receivers; multiple sessions are sampled up to the requested message count

---

## Contributing

Contributions are welcome! Open an issue or submit a pull request.

## License

MIT
