# PicoBusX 🚌

A minimalist **Azure Service Bus Explorer** built with ASP.NET Core Blazor Server (.NET 9).

---

## Features

- 🌲 **Interactive TreeView** — lists Queues, Topics, and Subscriptions (with filter/search)
- 📋 **Entity Details** — active message count, dead-letter count, lock duration, session info, timestamps
- 📤 **Send Message** — JSON editor with Format / Minify / Validate, optional headers, application properties
- 👁️ **Peek / Read Messages** — non-destructive peek or PeekLock receive, with expandable message cards (body pretty-printed if JSON)
- ✅ **Connection Status** — banner showing connected/not-connected with error details

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- An Azure Service Bus namespace with a connection string (Standard or Premium tier)

---

## How to Run

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
└── PicoBusX.Web/          # Blazor Server (.NET 9)
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

- **No Azure AD / Managed Identity** support yet — only connection-string auth (SAS). AAD auth can be added by injecting `TokenCredential` into `ServiceBusClientFactory`.
- **Peek is non-destructive** (uses `PeekMessages`). The "Receive" action uses PeekLock and immediately abandons messages after reading (non-destructive by default). Full consume is not available in this version.
- **No dead-letter browser** — to peek DLQ, the entity path must be manually set to `<queue>/$DeadLetterQueue`.
- **No message filtering** — peek returns the next N messages from the head of the queue/subscription.
- **No reconnect / retry UI** — if the connection string changes, restart the app.
- **Sessions** — session-aware entities require session-based receivers (not yet implemented).
