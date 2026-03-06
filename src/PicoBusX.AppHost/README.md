# PicoBusX AppHost

This is an **Aspire Host** project that orchestrates PicoBusX with an **Azure Service Bus Emulator**.

Built with **.NET 10** and **Aspire 13.1.2** with latest integrations.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (required to run Azure Service Bus Emulator)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/setup-tooling?tabs=windows)

Install the Aspire workload:

```bash
dotnet workload install aspire
```

## How to Run

### Option 1: Using Visual Studio / Rider

1. Open `PicoBusX.slnx` in Visual Studio or Rider
2. Set `PicoBusX.AppHost` as the startup project
3. Press `F5` to run

The Aspire **Dashboard** will automatically launch at `http://localhost:4317`, showing:
- The PicoBusX web app
- The Azure Service Bus Emulator resource
- Logs and traces for both services

### Option 2: Using dotnet CLI

```bash
cd src/PicoBusX.AppHost
dotnet run
```

The dashboard will be available at `http://localhost:4317`.

## Architecture

```
PicoBusX.AppHost (Aspire Host - Orchestration) [.NET 10 + Aspire 13.1.2]
├── serviceBus (Azure Service Bus Emulator running in Docker)
└── picobusx (PicoBusX Web Application)
    └── references → serviceBus
```

### Resources

| Resource | Type | Description |
|----------|------|-------------|
| `serviceBus` | Azure Service Bus Emulator | Runs in Docker container for local development |
| `picobusx` | Blazor Server Web App | PicoBusX web frontend, connected to Service Bus |

## Aspire Integrations

This project uses the latest Aspire integrations (v13.1.2):

### AppHost
- **`Aspire.Hosting`** (13.1.2) - Core Aspire hosting framework
- **`Aspire.Hosting.Azure.ServiceBus`** (13.1.2) - Azure Service Bus hosting and emulator support
  - Automatically manages the Service Bus Emulator container
  - Provides connection string discovery

### Client Service (PicoBusX.Web)
- **`Aspire.Azure.Messaging.ServiceBus`** (13.1.2) - Azure Messaging ServiceBus client integration
  - Automatic ServiceBusClient registration via `AddAzureServiceBusClient()`
  - Health checks included
  - Connection string injection from Aspire

## How It Works

1. **Service Discovery**: When running under Aspire, the connection string to the Service Bus Emulator is automatically injected into PicoBusX through the `ConnectionStrings:serviceBus` configuration key and the `AddAzureServiceBusClient()` integration.

2. **Dependency Management**: PicoBusX waits for the Service Bus Emulator to be ready before starting (`WaitFor` dependency).

3. **Local Emulation**: The Service Bus Emulator runs in a Docker container, providing a local Azure Service Bus-compatible endpoint for development and testing.

4. **Client Integration**: The `Aspire.Azure.Messaging.ServiceBus` integration automatically registers a typed `ServiceBusClient` in the dependency injection container.

## Configuration

The connection string is read from:

1. `ConnectionStrings:serviceBus` (from Aspire service discovery) ← **Highest priority**
2. `ASB_CONNECTION_STRING` environment variable
3. `ServiceBus:ConnectionString` in `appsettings.json`

> **Note**: When running under Aspire, priority #1 is used automatically.

## Troubleshooting

### Docker not running

If you see an error about Docker, ensure Docker Desktop is running:

```bash
docker ps
```

### Service Bus Emulator fails to start

Check if port 5671 (AMQP) and 5672 (AMQP no TLS) are available on your machine.

### Connection string errors

Verify the connection string format in the logs. When running under Aspire, it should look like:

```
Endpoint=sb://serviceBus-emulator/;SharedAccessKeyName=...;SharedAccessKey=...
```

## Aspire Documentation

- [Aspire Overview](https://aspire.dev)
- [Aspire Setup Tooling](https://learn.microsoft.com/en-us/dotnet/aspire/setup-tooling?tabs=windows)
- [Azure Service Bus Hosting Component](https://learn.microsoft.com/en-us/dotnet/aspire/components/aspire-hosting-azure-servicebus)
- [Azure Messaging ServiceBus Client Component](https://learn.microsoft.com/en-us/dotnet/aspire/components/aspire-azure-messaging-servicebus)
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

