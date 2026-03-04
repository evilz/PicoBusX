# Azure Service Bus Emulator - Admin Client Configuration

## ✅ Admin Client Works with Emulator!

The Azure Service Bus Emulator **fully supports management operations** via `ServiceBusAdministrationClient` (.NET only).

### Key Configuration Requirement

The emulator uses **port 5300** (by default) for management/administration operations. This port must be explicitly specified in the connection string.

## Port Configuration

| Operation Type | Protocol | Port | Connection String |
|---------------|----------|------|-------------------|
| **Messaging** | AMQP (sb://) | Dynamic | `Endpoint=sb://localhost;...` |
| **Management/Admin** | AMQP (sb://) | **5300** | `Endpoint=sb://localhost:5300;...` |

## Connection Strings

### For Messaging Operations (Send/Receive)
```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

### For Management Operations (Create/List/Delete Entities)
```
Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

**Notice:** The management connection string has **:5300** appended to `localhost`.

## How PicoBusX Handles This

`ServiceBusClientFactory` automatically detects the emulator and appends port 5300 to the endpoint:

```csharp
public ServiceBusAdministrationClient GetAdminClient()
{
    if (IsLikelyEmulator())
    {
        var uri = new Uri(endpoint); // sb://localhost
        var adminEndpoint = $"sb://{uri.Host}:5300"; // sb://localhost:5300
        
        // Rebuild connection string with :5300 port
        var adminConnectionString = $"Endpoint={adminEndpoint};SharedAccessKeyName=...;SharedAccessKey=...;UseDevelopmentEmulator=true";
        
        _adminClient = new ServiceBusAdministrationClient(adminConnectionString);
    }
}
```

### Detection Logic

The application detects emulator by checking for:
- `UseDevelopmentEmulator=true` in connection string
- `localhost` in endpoint
- `127.0.0.1` in endpoint

## Supported Operations

✅ **Entity Management** (.NET only)
- `GetQueuesAsync()` / `GetTopicsAsync()` / `GetSubscriptionsAsync()`
- `CreateQueueAsync()` / `CreateTopicAsync()` / `CreateSubscriptionAsync()`
- `QueueExistsAsync()` / `TopicExistsAsync()` / `SubscriptionExistsAsync()`
- `GetQueueAsync()` / `GetTopicAsync()` / `GetSubscriptionAsync()`
- `GetQueueRuntimePropertiesAsync()` - Message counts, statistics
- `UpdateQueueAsync()` / `UpdateTopicAsync()` / `UpdateSubscriptionAsync()`
- `DeleteQueueAsync()` / `DeleteTopicAsync()` / `DeleteSubscriptionAsync()`

✅ **Messaging Operations**
- Send, receive, peek messages
- Dead-letter queues
- Sessions

## Example: Creating Entities

```csharp
using Azure.Messaging.ServiceBus.Administration;

var adminConnectionString = 
    "Endpoint=sb://localhost:5300;" +
    "SharedAccessKeyName=RootManageSharedAccessKey;" +
    "SharedAccessKey=SAS_KEY_VALUE;" +
    "UseDevelopmentEmulator=true;";

var admin = new ServiceBusAdministrationClient(adminConnectionString);

// Create a queue
var queueName = "orders";
if (!await admin.QueueExistsAsync(queueName))
{
    await admin.CreateQueueAsync(new CreateQueueOptions(queueName)
    {
        DefaultMessageTimeToLive = TimeSpan.FromMinutes(30),
        LockDuration = TimeSpan.FromSeconds(45)
    });
}

// Create topic + subscription
var topicName = "events";
if (!await admin.TopicExistsAsync(topicName))
    await admin.CreateTopicAsync(topicName);

if (!await admin.SubscriptionExistsAsync(topicName, "all"))
    await admin.CreateSubscriptionAsync(topicName, "all");

// List all queues
await foreach (var q in admin.GetQueuesAsync())
    Console.WriteLine($"Queue: {q.Name}");

// Update queue properties
var qProps = await admin.GetQueueAsync(queueName);
qProps.Value.DefaultMessageTimeToLive = TimeSpan.FromHours(1);
await admin.UpdateQueueAsync(qProps);

// Cleanup
await admin.DeleteQueueAsync(queueName);
await admin.DeleteTopicAsync(topicName);
```

## Usage with Aspire

When running PicoBusX via Aspire:

```bash
cd src/PicoBusX.AppHost
dotnet run
```

Aspire automatically:
1. Starts the Service Bus emulator in Docker
2. Injects connection string with `UseDevelopmentEmulator=true`
3. PicoBusX detects this and configures port 5300 for admin operations

**Result:** Full admin functionality works out of the box!

## Troubleshooting

### Port 5300 Not Available

Check if port is in use:
```powershell
netstat -ano | findstr "5300"
```

### Connection Errors

Verify:
- Docker Desktop is running
- Emulator container is healthy: `docker ps`
- Connection string includes `UseDevelopmentEmulator=true`

### Platform Limitation

**Important:** Management operations are **only supported in .NET**. Other platforms/languages may not support emulator admin operations.

## Custom Port Configuration

You can configure the emulator to use a different management port. If using a custom port, ensure you update the connection string accordingly:

```
Endpoint=sb://localhost:CUSTOM_PORT;...;UseDevelopmentEmulator=true;
```

PicoBusX currently defaults to port 5300. For custom ports, you may need to configure this manually.

## References

- [Azure Service Bus Emulator Documentation](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator)
- [Managing Entities in Emulator](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator#create-and-manage-entities-within-service-bus-emulator)
- [Aspire Service Bus Integration](https://learn.microsoft.com/en-us/dotnet/aspire/components/aspire-hosting-azure-servicebus)

## Summary

| Configuration | Value |
|--------------|-------|
| **Protocol** | AMQP (sb://) |
| **Management Port** | 5300 (default) |
| **Messaging Port** | Dynamic |
| **Required Flag** | `UseDevelopmentEmulator=true` |
| **Platform** | .NET only |
| **Auto-config** | ✅ Yes (PicoBusX) |

PicoBusX handles all the configuration automatically! 🎉

