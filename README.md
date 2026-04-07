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
- 🛠️ **Entity Management** — create and delete Queues, Topics, and Subscriptions directly from the UI
- 📤 **Send Message** — JSON editor with Format / Minify / Validate, optional headers, application properties, and optional scheduled enqueue time
- 👁️ **Peek / Read Messages** — non-destructive peek or PeekLock receive, with expandable message cards (body pretty-printed if JSON)
- ☠️ **Dead-Letter Queue Browser** — dedicated DLQ tab to peek dead-letter messages and resubmit them to the main queue
- 🔍 **Client-side Message Filter** — filter loaded messages by MessageId, Subject, CorrelationId, SessionId, Body, or Application Properties
- ⬇️ **Load More / Pagination** — incrementally load additional messages beyond the initial batch
- ⚙️ **Runtime Connection Settings** — configure the Service Bus connection string or Service Principal via the `/settings` page (persisted to disk, no restart required)
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
| `ServiceBus__SERVICEBUS_CONNECTIONSTRING` | ✅ Yes | — | Azure Service Bus connection string |
| `ServiceBus__TransportType` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |
| `ServiceBus__EntityMaxPeek` | No | `10` | Default max messages for Peek/Receive |

### Alternative: appsettings.json

```json
{
  "ServiceBus": {
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
│   ├── Program.cs             # Aspire orchestration configuration
│   ├── PicoBusX.AppHost.csproj
│   └── README.md              # Detailed Aspire documentation
│
└── PicoBusX.Web/              # Blazor Server (.NET 10)
    ├── Components/
    │   ├── Pages/
    │   │   ├── Home.razor             # Main dashboard (tree + details + send + peek/DLQ)
    │   │   └── Settings.razor         # Runtime connection settings page
    │   ├── Layout/
    │   │   └── MainLayout.razor       # Minimal dark-header layout
    │   ├── BusTreeView.razor          # Collapsible tree with search
    │   ├── DlqPanel.razor             # Dead-letter queue browser + resubmit
    │   ├── EntityDetailsPanel.razor   # Queue/Topic/Subscription property tables
    │   ├── JsonMessageEditor.razor    # JSON textarea editor (format/minify/validate/schedule)
    │   ├── MessageCard.razor          # Expandable message card (shared)
    │   ├── MessageList.razor          # Message list rendering (shared by Peek and DLQ panels)
    │   ├── MessagePanelBase.cs        # Shared base class (state, filter, pagination)
    │   └── PeekReadPanel.razor        # Peek / Receive message browser
    ├── Models/                        # QueueInfo, TopicInfo, BrowsedMessage, etc.
    ├── Options/
    │   └── ServiceBusConnectionOptions.cs
    ├── Services/
    │   ├── ServiceBusClientFactory.cs  # Singleton client/admin client factory
    │   ├── ConnectionSettingsStore.cs  # Persist runtime connection settings to disk
    │   ├── EntityManagementService.cs  # Create / delete queues, topics, subscriptions
    │   ├── ExplorerService.cs          # List entities + runtime properties
    │   ├── MessageSenderService.cs     # Send JSON messages (including scheduled)
    │   └── MessageBrowserService.cs    # Peek / Receive / DLQ messages
    ├── Program.cs
    └── appsettings.json
```

---

## Known Limits

- **Azure Service Bus Emulator** — ✅ Supported when running under Aspire
- **No Azure AD / Managed Identity** support yet — only connection-string auth (SAS)
- **Peek is non-destructive** — uses `PeekMessages`; Receive uses PeekLock and abandons immediately
- **No reconnect / retry UI** — restart the app if the connection string changes at the OS level (runtime settings changes via `/settings` do take effect immediately)
- **Sessions** — session-enabled queues/subscriptions are browsed via session receivers; multiple sessions are sampled up to the requested message count

---

## Contributing

Contributions are welcome! Open an issue or submit a pull request.

## License

MIT
