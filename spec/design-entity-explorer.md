---
title: Service Bus Entity Explorer — Display, Navigation & Metrics
version: 1.0
date_created: 2026-03-06
last_updated: 2026-03-06
owner: PicoBusX maintainers
tags: [design, app, azure-service-bus, explorer, ui]
---

# Introduction

This specification defines the requirements for the **Entity Explorer** feature of PicoBusX, a Blazor Server application for browsing and operating on Azure Service Bus namespaces. The Entity Explorer is the primary navigation surface of the application: it presents a hierarchical tree of all Service Bus entities within a configured namespace and, when an entity is selected, displays its full metadata, configuration properties, and runtime message metrics.

## 1. Purpose & Scope

### Purpose

Define the complete set of entities to display, the properties and metrics to expose per entity type, the navigation model, and the data contracts that back the UI.

### Scope

| In scope | Out of scope |
|---|---|
| Azure Service Bus **Queues** | Azure Relay (separate service) |
| Azure Service Bus **Topics** | Azure Event Hubs (separate service) |
| Azure Service Bus **Subscriptions** | Azure Notification Hubs (separate service) |
| **Dead-letter sub-queues** (as virtual nodes) | Message filter/action rules (handled by a separate spec) |
| **Transfer dead-letter sub-queues** | Long-term time-series metrics (Azure Monitor integration) |
| Entity-level runtime metrics (message counts, size) | |
| Entity configuration properties (TTL, sessions, partitioning, …) | |

> **Note:** Azure Relay, Event Hubs, and Notification Hubs are distinct Azure services that are not addressable through the `Azure.Messaging.ServiceBus` SDK. They are explicitly excluded from this specification.

### Intended Audience

- Blazor UI developers implementing or extending the Explorer components
- Backend service developers maintaining `ExplorerService` and related models
- AI code-generation tools producing code consistent with PicoBusX conventions

### Assumptions

- The application is connected to a single Azure Service Bus namespace per session (configured via `ServiceBusConnectionOptions`).
- The `ServiceBusAdministrationClient` is used for all metadata and configuration retrieval.
- Runtime metrics are retrieved via `GetQueueRuntimePropertiesAsync`, `GetTopicRuntimePropertiesAsync`, and `GetSubscriptionRuntimePropertiesAsync`.
- The UI framework is Microsoft FluentUI for Blazor (`Microsoft.FluentUI.AspNetCore.Components`).

---

## 2. Definitions

| Term | Definition |
|---|---|
| **Entity** | A named Service Bus resource within a namespace: Queue, Topic, or Subscription. |
| **Queue** | A point-to-point messaging channel. Messages are consumed by a single receiver. |
| **Topic** | A publish/subscribe messaging channel. Publishers send to a Topic; consumers receive via Subscriptions. |
| **Subscription** | A named view into a Topic. Each Subscription maintains its own message copy. |
| **Dead-Letter Queue (DLQ)** | A virtual sub-queue (suffix `/$DeadLetterQueue`) that stores messages that could not be delivered or that expired. Every Queue and Subscription has its own DLQ. |
| **Transfer Dead-Letter Queue (TDLQ)** | A virtual sub-queue (suffix `/$Transfer/$DeadLetterQueue`) that stores messages that failed during forwarding. Only Queues expose this count. |
| **TTL (Time-To-Live)** | The maximum duration a message lives before it is moved to the DLQ or discarded, expressed as a `TimeSpan`. Configurable at the entity level (`DefaultMessageTimeToLive`) and overridable per message. |
| **Lock Duration** | The duration a message is locked for exclusive consumption after being received. Receivers must complete or abandon the message before the lock expires. |
| **Max Delivery Count** | The maximum number of times a message can be delivered. Exceeding this count moves the message to the DLQ. |
| **Sessions** | An optional feature (`RequiresSession`) that groups related messages into ordered, exclusive processing streams identified by a `SessionId`. |
| **Partitioning** | An optional feature (`EnablePartitioning`) that distributes an entity's data across multiple message brokers for higher throughput. |
| **Batched Operations** | An optional performance feature (`EnableBatchedOperations`) that batches acknowledgements to improve throughput. |
| **Runtime Properties** | Dynamic, read-only properties computed at query time: message counts, entity size in bytes, created/updated timestamps. |
| **Configuration Properties** | Static properties set at entity creation or via update: TTL, lock duration, session enablement, partitioning, etc. |
| **BusEntity** | The application model (`PicoBusX.Web.Models.BusEntity`) representing a selected entity in the UI: type, name, optional topic name, and entity path. |
| **EntityPath** | The AMQP path used to address an entity: `<name>` for Queues and Topics, `<topicName>/subscriptions/<name>` for Subscriptions. |
| **ASB** | Azure Service Bus |
| **DLQ** | Dead-Letter Queue |
| **SDK** | Azure SDK for .NET (`Azure.Messaging.ServiceBus`) |

---

## 3. Requirements, Constraints & Guidelines

### Functional Requirements

- **REQ-001**: The Explorer MUST display all Queues, Topics, and Subscriptions available in the configured namespace.
- **REQ-002**: Queues MUST be presented as a collapsible group in the tree.
- **REQ-003**: Topics MUST be presented as a collapsible group; each Topic node MUST be expandable to reveal its child Subscriptions inline.
- **REQ-004**: Each Queue node MUST display a badge showing the number of active messages when greater than zero.
- **REQ-005**: Each Queue node MUST display a distinct badge showing the number of dead-letter messages when greater than zero.
- **REQ-006**: Each Subscription node MUST display active and dead-letter message count badges following the same rules as REQ-004 and REQ-005.
- **REQ-007**: Selecting any node (Queue, Topic, Subscription) MUST populate the details panel with the entity's configuration properties and runtime metrics.
- **REQ-008**: The tree MUST support text filtering: entering a string MUST hide non-matching nodes while preserving group headers.
- **REQ-009**: The details panel for a **Queue** MUST display all properties listed in Section 4 under `QueueInfo`.
- **REQ-010**: The details panel for a **Topic** MUST display all properties listed in Section 4 under `TopicInfo`, and a summary table of its child Subscriptions.
- **REQ-011**: The details panel for a **Subscription** MUST display all properties listed in Section 4 under `SubscriptionInfo`.
- **REQ-012**: The tree MUST show a loading indicator while data is being fetched.
- **REQ-013**: The tree MUST show an error message when data retrieval fails, instead of an empty tree.
- **REQ-014**: A Refresh action MUST reload all entity data from the namespace without requiring a full page reload.
- **REQ-015**: The `ExplorerService` MUST expose the missing configuration properties (`DefaultMessageTimeToLive`, `EnablePartitioning`, `EnableBatchedOperations`, `AutoDeleteOnIdle`, `ForwardTo`, `ForwardDeadLetteredMessagesTo`) in the corresponding `*Info` models.

### Security Requirements

- **SEC-001**: No message payload data MUST be loaded or displayed in the Explorer tree or details panel. The Explorer is metadata-only; message content is exclusively handled by the message browsing feature.
- **SEC-002**: Connection string credentials MUST NOT be transmitted to the browser or exposed in component parameters.

### Constraints

- **CON-001**: The Explorer MUST function using only the `ServiceBusAdministrationClient` API; it MUST NOT use the `ServiceBusClient` (sender/receiver) API for metadata retrieval.
- **CON-002**: The application targets a single namespace per application instance; multi-namespace exploration is out of scope.
- **CON-003**: Runtime properties (message counts, sizes) are fetched at load time and on explicit refresh only; they are NOT updated in real time via polling or push.
- **CON-004**: Dead-letter sub-queues are virtual nodes in the UI only (badge on parent). They are not independent tree nodes; they are accessible via the "Dead Letter" tab on the parent entity.
- **CON-005**: Entity types outside the Azure Service Bus namespace (Relay, Event Hubs, Notification Hubs) MUST NOT be listed, as they are not addressable by the SDK in use.

### Guidelines

- **GUD-001**: Use `async`/`await` with `CancellationToken` for all service calls; do not block the Blazor render thread.
- **GUD-002**: Handle `ServiceBusException` and `RequestFailedException` separately from network connectivity errors. Connectivity failures should degrade gracefully (empty list + warning); authentication and configuration failures should surface a clear error message.
- **GUD-003**: Display entity sizes in human-readable format (B / KB / MB / GB) using the existing `FormatSize` helper.
- **GUD-004**: Display timestamps in ISO 8601 format with timezone offset (e.g., `2024-01-15 10:30:00 +00:00`).
- **GUD-005**: `TimeSpan` properties (TTL, LockDuration, AutoDeleteOnIdle) MUST be displayed in a human-readable form (e.g., `10 minutes 30 seconds`), not raw ticks or ISO 8601 duration strings.
- **GUD-006**: Properties that are `null` or unavailable MUST display as `—` (em dash), not empty strings or zeros.

### Patterns

- **PAT-001**: Follow the existing `ExplorerService` → Razor component data-flow pattern: services return typed model lists; components bind to those models via `[Parameter]`.
- **PAT-002**: All property additions to `QueueInfo`, `TopicInfo`, and `SubscriptionInfo` MUST remain plain C# records or classes with no Azure SDK type references — all mapping happens in `ExplorerService`.

---

## 4. Interfaces & Data Contracts

### 4.1 Model: `QueueInfo`

All properties to be exposed by the `QueueInfo` model (located at `PicoBusX.Web.Models/QueueInfo.cs`):

| Property | Type | Source (SDK property) | Description |
|---|---|---|---|
| `Name` | `string` | `QueueProperties.Name` | Queue name |
| `ActiveMessageCount` | `long` | `QueueRuntimeProperties.ActiveMessageCount` | Messages available for delivery |
| `DeadLetterMessageCount` | `long` | `QueueRuntimeProperties.DeadLetterMessageCount` | Messages in the DLQ |
| `TransferDeadLetterMessageCount` | `long` | `QueueRuntimeProperties.TransferDeadLetterMessageCount` | Messages in the Transfer DLQ |
| `ScheduledMessageCount` | `long` | `QueueRuntimeProperties.ScheduledMessageCount` | Scheduled (future delivery) messages |
| `TransferMessageCount` | `long` | `QueueRuntimeProperties.TransferMessageCount` | Messages pending forwarding |
| `SizeInBytes` | `long` | `QueueRuntimeProperties.SizeInBytes` | Current entity size |
| `CreatedAt` | `DateTimeOffset?` | `QueueRuntimeProperties.CreatedAt` | Entity creation timestamp |
| `UpdatedAt` | `DateTimeOffset?` | `QueueRuntimeProperties.UpdatedAt` | Last update timestamp |
| `AccessedAt` | `DateTimeOffset?` | `QueueRuntimeProperties.AccessedAt` | Last access timestamp |
| `LockDuration` | `TimeSpan` | `QueueProperties.LockDuration` | Message lock duration |
| `MaxDeliveryCount` | `int` | `QueueProperties.MaxDeliveryCount` | Max delivery attempts before DLQ |
| `RequiresSession` | `bool` | `QueueProperties.RequiresSession` | Session-enabled flag |
| `MaxSizeInMegabytes` | `long` | `QueueProperties.MaxSizeInMegabytes` | Configured maximum size |
| `DefaultMessageTimeToLive` | `TimeSpan` | `QueueProperties.DefaultMessageTimeToLive` | Default message TTL |
| `AutoDeleteOnIdle` | `TimeSpan` | `QueueProperties.AutoDeleteOnIdle` | Idle-deletion timeout |
| `EnablePartitioning` | `bool` | `QueueProperties.EnablePartitioning` | Whether partitioning is enabled |
| `EnableBatchedOperations` | `bool` | `QueueProperties.EnableBatchedOperations` | Whether batched acknowledgements are enabled |
| `ForwardTo` | `string?` | `QueueProperties.ForwardTo` | Auto-forward destination entity name |
| `ForwardDeadLetteredMessagesTo` | `string?` | `QueueProperties.ForwardDeadLetteredMessagesTo` | Auto-forward DLQ destination |
| `DeadLetteringOnMessageExpiration` | `bool` | `QueueProperties.DeadLetteringOnMessageExpiration` | Move expired messages to DLQ |
| `Status` | `string` | `QueueProperties.Status` | Entity status (`Active`, `Disabled`, `SendDisabled`, `ReceiveDisabled`) |

### 4.2 Model: `TopicInfo`

| Property | Type | Source (SDK property) | Description |
|---|---|---|---|
| `Name` | `string` | `TopicProperties.Name` | Topic name |
| `SizeInBytes` | `long` | `TopicRuntimeProperties.SizeInBytes` | Current entity size |
| `ScheduledMessageCount` | `long` | `TopicRuntimeProperties.ScheduledMessageCount` | Scheduled messages across all subscriptions |
| `CreatedAt` | `DateTimeOffset?` | `TopicRuntimeProperties.CreatedAt` | Creation timestamp |
| `UpdatedAt` | `DateTimeOffset?` | `TopicRuntimeProperties.UpdatedAt` | Last update timestamp |
| `AccessedAt` | `DateTimeOffset?` | `TopicRuntimeProperties.AccessedAt` | Last access timestamp |
| `MaxSizeInMegabytes` | `long` | `TopicProperties.MaxSizeInMegabytes` | Configured maximum size |
| `DefaultMessageTimeToLive` | `TimeSpan` | `TopicProperties.DefaultMessageTimeToLive` | Default message TTL |
| `AutoDeleteOnIdle` | `TimeSpan` | `TopicProperties.AutoDeleteOnIdle` | Idle-deletion timeout |
| `EnablePartitioning` | `bool` | `TopicProperties.EnablePartitioning` | Whether partitioning is enabled |
| `EnableBatchedOperations` | `bool` | `TopicProperties.EnableBatchedOperations` | Whether batched acknowledgements are enabled |
| `SupportOrdering` | `bool` | `TopicProperties.SupportOrdering` | Whether message ordering is supported |
| `DuplicateDetectionHistoryTimeWindow` | `TimeSpan` | `TopicProperties.DuplicateDetectionHistoryTimeWindow` | Duplicate detection window |
| `RequiresDuplicateDetection` | `bool` | `TopicProperties.RequiresDuplicateDetection` | Whether duplicate detection is enabled |
| `Status` | `string` | `TopicProperties.Status` | Entity status |
| `Subscriptions` | `List<SubscriptionInfo>` | (aggregated) | Child subscriptions |

### 4.3 Model: `SubscriptionInfo`

| Property | Type | Source (SDK property) | Description |
|---|---|---|---|
| `TopicName` | `string` | (context) | Parent topic name |
| `Name` | `string` | `SubscriptionProperties.SubscriptionName` | Subscription name |
| `ActiveMessageCount` | `long` | `SubscriptionRuntimeProperties.ActiveMessageCount` | Active messages |
| `DeadLetterMessageCount` | `long` | `SubscriptionRuntimeProperties.DeadLetterMessageCount` | DLQ messages |
| `TransferMessageCount` | `long` | `SubscriptionRuntimeProperties.TransferMessageCount` | Messages pending forwarding |
| `TransferDeadLetterMessageCount` | `long` | `SubscriptionRuntimeProperties.TransferDeadLetterMessageCount` | Transfer DLQ messages |
| `CreatedAt` | `DateTimeOffset?` | `SubscriptionRuntimeProperties.CreatedAt` | Creation timestamp |
| `UpdatedAt` | `DateTimeOffset?` | `SubscriptionRuntimeProperties.UpdatedAt` | Last update timestamp |
| `AccessedAt` | `DateTimeOffset?` | `SubscriptionRuntimeProperties.AccessedAt` | Last access timestamp |
| `LockDuration` | `TimeSpan` | `SubscriptionProperties.LockDuration` | Message lock duration |
| `MaxDeliveryCount` | `int` | `SubscriptionProperties.MaxDeliveryCount` | Max delivery attempts before DLQ |
| `RequiresSession` | `bool` | `SubscriptionProperties.RequiresSession` | Session-enabled flag |
| `DefaultMessageTimeToLive` | `TimeSpan` | `SubscriptionProperties.DefaultMessageTimeToLive` | Default message TTL |
| `AutoDeleteOnIdle` | `TimeSpan` | `SubscriptionProperties.AutoDeleteOnIdle` | Idle-deletion timeout |
| `EnableBatchedOperations` | `bool` | `SubscriptionProperties.EnableBatchedOperations` | Batched acknowledgements |
| `DeadLetteringOnMessageExpiration` | `bool` | `SubscriptionProperties.DeadLetteringOnMessageExpiration` | Move expired messages to DLQ |
| `DeadLetteringOnFilterEvaluationExceptions` | `bool` | `SubscriptionProperties.DeadLetteringOnFilterEvaluationExceptions` | Move filter-error messages to DLQ |
| `ForwardTo` | `string?` | `SubscriptionProperties.ForwardTo` | Auto-forward destination |
| `ForwardDeadLetteredMessagesTo` | `string?` | `SubscriptionProperties.ForwardDeadLetteredMessagesTo` | DLQ auto-forward destination |
| `Status` | `string` | `SubscriptionProperties.Status` | Entity status |

### 4.4 `BusEntity` Model (unchanged)

```csharp
public enum BusEntityType { Queue, Topic, Subscription }

public class BusEntity
{
    public BusEntityType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TopicName { get; set; }
    public string EntityPath => Type == BusEntityType.Subscription
        ? $"{TopicName}/subscriptions/{Name}"
        : Name;
}
```

### 4.5 `ExplorerService` Interface

```csharp
// Returns all queues in the namespace with full configuration + runtime properties.
Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default);

// Returns all topics with their subscriptions, including full configuration + runtime properties.
Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default);
```

### 4.6 UI Components

| Component | File | Responsibility |
|---|---|---|
| `BusTreeView` | `Components/BusTreeView.razor` | Renders the hierarchical entity tree with filtering and selection |
| `EntityDetailsPanel` | `Components/EntityDetailsPanel.razor` | Renders the configuration and metrics table for the selected entity |
| `Home` (page) | `Components/Pages/Home.razor` | Orchestrates data loading, selection state, and tab routing |

---

## 5. Acceptance Criteria

- **AC-001**: Given a namespace with 3 queues and 2 topics (each with 2 subscriptions), when the app loads, then the tree shows a "Queues (3)" group and a "Topics (2)" group, each expandable.
- **AC-002**: Given a queue with 5 active messages and 2 dead-letter messages, when the tree renders, then the queue node displays an accent badge "5" and an orange badge "DL:2".
- **AC-003**: Given a queue node is selected, when the details panel renders, then it shows all properties defined in Section 4.1 with no missing rows.
- **AC-004**: Given a topic node is selected, when the details panel renders, then it shows topic properties (Section 4.2) and a subscriptions summary table.
- **AC-005**: Given a subscription node is selected, when the details panel renders, then it shows all properties defined in Section 4.3.
- **AC-006**: Given the user types "order" in the filter input, when the tree re-renders, then only queue/topic/subscription names containing "order" (case-insensitive) are shown; groups remain visible with updated counts.
- **AC-007**: Given the user clears the filter, when the tree re-renders, then all entities are shown again.
- **AC-008**: Given a `TTL` value of `P10675199DT2H48M5.4775807S` (maximum TTL / "no expiry"), when rendered in the details panel, then it MUST display as `Never` rather than a raw duration string.
- **AC-009**: Given a property that is `null` or unavailable at load time, when rendered in the details panel, then the value cell displays `—`.
- **AC-010**: Given the Service Bus admin endpoint is unreachable (network timeout), when the tree loads, then it shows a warning message explaining connectivity failure and does not throw an unhandled exception.
- **AC-011**: Given the user clicks Refresh, when data reloads, then the tree and all detail panels reflect updated message counts.
- **AC-012**: Given a `ForwardTo` property with a value, when rendered in the details panel, then it is displayed as a readable string showing the forwarding destination.

---

## 6. Test Automation Strategy

- **Test Levels**: Unit (service layer), Blazor component rendering (bunit)
- **Frameworks**: MSTest or xUnit, FluentAssertions, Moq (for `ServiceBusAdministrationClient` mocking via wrapper interface if needed)
- **Test Data Management**: Use in-memory fake `ServiceBusAdministrationClient` responses or a mock factory returning pre-built `QueueProperties` / `QueueRuntimeProperties` objects.
- **CI/CD Integration**: Tests run in GitHub Actions on every push and pull request.
- **Coverage Requirements**: Minimum 80% line coverage for `ExplorerService` and model mapping code.
- **Key Test Scenarios**:
  - `GetQueuesAsync` maps all `QueueInfo` properties correctly from SDK types.
  - `GetTopicsAsync` maps all `TopicInfo` and nested `SubscriptionInfo` properties correctly.
  - Connectivity errors return an empty list with a logged warning (no exception thrown).
  - Authentication/configuration errors propagate as exceptions.
  - `BusTreeView` renders correct badge counts given mocked queue/topic data.
  - `EntityDetailsPanel` renders `—` for null properties.
  - `EntityDetailsPanel` renders `Never` for maximum-value `TimeSpan` TTL.

---

## 7. Rationale & Context

### Why expand the property set now?

The current models expose a subset of available properties. Operators routinely need to inspect TTL, partitioning, session requirements, and forwarding configuration when diagnosing messaging issues. Exposing these properties in the Explorer eliminates the need to switch to the Azure Portal for basic configuration lookup.

### Why exclude Relay / Event Hubs / Notification Hubs?

These are independent Azure services with separate SDKs and connection primitives. Including them would require separate client configurations and authentication flows, making the scope of the Explorer significantly larger. They are candidates for future, separately scoped features.

### Why no real-time metrics?

Azure Service Bus does not offer a streaming metrics API in the administration SDK. Polling at sub-second intervals would generate excessive API calls. Time-series and near-real-time metrics require Azure Monitor integration, which is a separate architectural concern beyond the Explorer's scope.

### Why show DLQ as a badge rather than a tree node?

Dead-letter sub-queues share the parent entity's connection path (appended with `/$DeadLetterQueue`). They are not independently named or listable via the administration API. Representing them as a dedicated tab on the parent entity is consistent with the Azure Service Bus Explorer and Azure Portal UX conventions.

---

## 8. Dependencies & External Integrations

### External Systems

- **EXT-001**: Azure Service Bus namespace — provides the administration API for listing entities, retrieving configuration properties, and runtime metrics.

### Third-Party Services

- **SVC-001**: Azure Service Bus Administration REST API (via SDK) — must be reachable from the application host over HTTPS (port 443) or AMQP WebSockets.

### Infrastructure Dependencies

- **INF-001**: Azure Service Bus Emulator (local dev) — supports listing queues and topics; may not expose all runtime property fields; the Explorer must handle partial data gracefully.

### Technology Platform Dependencies

- **PLT-001**: .NET 10 (Blazor Server interactive render mode)
- **PLT-002**: `Azure.Messaging.ServiceBus` SDK — provides `ServiceBusAdministrationClient`, `QueueProperties`, `QueueRuntimeProperties`, `TopicProperties`, `TopicRuntimeProperties`, `SubscriptionProperties`, `SubscriptionRuntimeProperties`
- **PLT-003**: Microsoft FluentUI for Blazor — used for tree view, badges, cards, tables, and progress indicators

---

## 9. Examples & Edge Cases

### Edge Case: Maximum TTL ("No Expiry")

The Azure Service Bus SDK represents "no TTL expiry" as `TimeSpan.MaxValue` (`P10675199DT2H48M5.4775807S`). The UI MUST detect this sentinel value and display `Never`.

```csharp
private static string FormatTimeSpan(TimeSpan ts)
{
    if (ts == TimeSpan.MaxValue) return "Never";
    if (ts == TimeSpan.Zero) return "0 seconds";

    var parts = new List<string>();
    if (ts.Days > 0) parts.Add($"{ts.Days} day{(ts.Days == 1 ? "" : "s")}");
    if (ts.Hours > 0) parts.Add($"{ts.Hours} hour{(ts.Hours == 1 ? "" : "s")}");
    if (ts.Minutes > 0) parts.Add($"{ts.Minutes} minute{(ts.Minutes == 1 ? "" : "s")}");
    if (ts.Seconds > 0) parts.Add($"{ts.Seconds} second{(ts.Seconds == 1 ? "" : "s")}");
    return string.Join(" ", parts);
}
```

### Edge Case: Emulator Partial Data

The Azure Service Bus Emulator may return `null` for `AccessedAt` and default `DateTimeOffset.MinValue` for creation timestamps. These MUST be normalised to `null` in the mapping layer:

```csharp
CreatedAt = runtime?.CreatedAt == DateTimeOffset.MinValue ? null : runtime?.CreatedAt,
```

### Edge Case: Forwarding Chains

When `ForwardTo` is set on a Queue or Subscription, the value is the name of another entity in the same namespace. It MUST be displayed as plain text; there is no requirement to resolve or navigate to the forwarding target from the details panel at this time.

### Edge Case: Large Entity Count

Namespaces may contain thousands of entities. The tree MUST render all entities without client-side pagination, but the text filter MUST efficiently reduce the visible set. No virtual scrolling is required in this specification version.

---

## 10. Validation Criteria

- All properties listed in Section 4 are present in the corresponding model class.
- `ExplorerService.GetQueuesAsync` maps every non-null `QueueProperties` and `QueueRuntimeProperties` field to `QueueInfo`.
- `ExplorerService.GetTopicsAsync` maps every non-null `TopicProperties`, `TopicRuntimeProperties`, `SubscriptionProperties`, and `SubscriptionRuntimeProperties` field to the corresponding info model.
- `EntityDetailsPanel` renders a table row for every property in the model; no property is silently omitted.
- `TimeSpan.MaxValue` renders as `Never` in all property rows.
- `null` optional properties render as `—`.
- The tree filter is case-insensitive and matches partial names.
- Network connectivity errors produce a visible warning in the tree panel; the application does not crash.
- Authentication errors (HTTP 401/403) produce a distinct, actionable error message.
- All unit tests for `ExplorerService` pass with mocked SDK responses.

---

## 11. Related Specifications / Further Reading

- [Azure Service Bus Administration Client documentation](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.servicebusadministrationclient)
- [QueueProperties reference](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.queueproperties)
- [TopicProperties reference](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.topicproperties)
- [SubscriptionProperties reference](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.subscriptionproperties)
- [QueueRuntimeProperties reference](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.administration.queueruntimeproperties)
- [Microsoft FluentUI for Blazor — FluentTreeView](https://www.fluentui-blazor.net/TreeView)
