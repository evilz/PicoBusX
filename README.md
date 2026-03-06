# PicoBusX 🚌

A minimalist **Azure Service Bus Explorer** built with ASP.NET Core Blazor Server (.NET 10).

Built with latest **Aspire 13.1.2** for local development orchestration and **Microsoft FluentUI Blazor** for a dashboard UI inspired by the .NET Aspire Dashboard.

![PicoBusX Dashboard](https://github.com/user-attachments/assets/cc13c01d-4860-4701-b6db-1b59db5968d3)

---

## Features

- 🌲 **Interactive TreeView** — lists Queues, Topics, and Subscriptions (with filter/search)
- 📋 **Entity Details** — active message count, dead-letter count, lock duration, session info, timestamps
- 📤 **Send Message** — JSON editor with Format / Minify / Validate, optional headers, application properties
- 👁️ **Peek / Read Messages** — non-destructive peek or PeekLock receive, with expandable message cards (body pretty-printed if JSON)
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

**Features:**
- 🚀 Automatic Service Bus Emulator orchestration
- 📊 Real-time Aspire Dashboard at `http://localhost:4317`
- 🔗 Automatic service discovery and connection string injection
- 🔧 Client integration via `Aspire.Azure.Messaging.ServiceBus`
- 📈 Built-in health checks and observability

**Steps:**
```bash
# Clone the repository
git clone https://github.com/evilz/PicoBusX.git
cd PicoBusX

# Set PicoBusX.AppHost as the startup project and run
# (In Visual Studio / Rider: Select PicoBusX.AppHost and press F5)
cd src/PicoBusX.AppHost
dotnet run
```

The **Aspire Dashboard** will automatically open at `http://localhost:4317`, showing:
- PicoBusX web app
- Azure Service Bus Emulator status and logs
- Real-time traces and observability data

Then visit: **http://localhost:5000** (or the endpoint shown in dashboard)

See [PicoBusX.AppHost README](./src/PicoBusX.AppHost/README.md) for more details on Aspire integrations and configuration.

### Option 2: Manual Setup (Standalone)

Use your own Azure Service Bus namespace or emulator.

```bash
# Clone the repository
git clone https://github.com/evilz/PicoBusX.git
cd PicoBusX

# Set your connection string (option A: environment variable)
export ASB_CONNECTION_STRING="Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=..."

# Run the app
cd src/PicoBusX.Web
dotnet run
```

Then open your browser at: **http://localhost:5000**

---

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ASB_CONNECTION_STRING` | ✅ Yes | — | Azure Service Bus connection string |
| `ASB_TRANSPORT_TYPE` | No | `AmqpTcp` | `AmqpTcp` or `AmqpWebSockets` |
| `ASB_ENTITY_MAX_PEEK` | No | `10` | Default max messages for Peek/Receive |

### Alternative: appsettings.json

You can also set the connection string in `src/PicoBusX.Web/appsettings.json`:

```json
{
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;...",
    "TransportType": "AmqpTcp",
    "EntityMaxPeek": 10
  }
}
```

> **⚠️ Never commit credentials to source control.** Prefer environment variables or user secrets for local development.

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
    │   │   └── Home.razor             # Main dashboard (tree + details + send + peek)
    │   ├── Layout/
    │   │   └── MainLayout.razor       # Minimal dark-header layout
    │   ├── BusTreeView.razor          # Collapsible tree with search
    │   ├── EntityDetailsPanel.razor   # Queue/Topic/Subscription property tables
    │   ├── JsonMessageEditor.razor    # JSON textarea editor (format/minify/validate)
    │   └── PeekReadPanel.razor        # Peek / Receive message browser
    ├── Models/                        # QueueInfo, TopicInfo, BrowsedMessage, etc.
    ├── Options/
    │   └── ServiceBusConnectionOptions.cs
    ├── Services/
    │   ├── ServiceBusClientFactory.cs # Singleton client/admin client factory
    │   ├── ExplorerService.cs         # List entities + runtime properties
    │   ├── MessageSenderService.cs    # Send JSON messages
    │   └── MessageBrowserService.cs   # Peek / Receive messages
    ├── Program.cs
    └── appsettings.json
```

---

## Known Limits

- **Azure Service Bus Emulator** - ✅ **Supported** when running under Aspire (port 5300 is exposed for admin/management operations). All CRUD operations and entity management work with the emulator.
- **No Azure AD / Managed Identity** support yet — only connection-string auth (SAS). AAD auth can be added by injecting `TokenCredential` into `ServiceBusClientFactory`.
- **Peek is non-destructive** (uses `PeekMessages`). The "Receive" action uses PeekLock and immediately abandons messages after reading (non-destructive by default). Full consume is not available in this version.
- **No dead-letter browser** — to peek DLQ, the entity path must be manually set to `<queue>/$DeadLetterQueue`.
- **No message filtering** — peek returns the next N messages from the head of the queue/subscription.
- **No reconnect / retry UI** — if the connection string changes, restart the app.
- **Sessions** — session-aware entities require session-based receivers (not yet implemented).
