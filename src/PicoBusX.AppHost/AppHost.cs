﻿var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Service Bus with emulator support
// Expose port 5300 for admin/management operations using WithEndpoint
 var serviceBus = builder
     .AddAzureServiceBus("serviceBus")
     .RunAsEmulator(resourceBuilder => resourceBuilder.WithImageTag("latest"));

// Pre-create default entities for exploration and testing
serviceBus.AddServiceBusQueue("Queue1");
serviceBus.AddServiceBusQueue("Queue2");

var topic1 = serviceBus.AddServiceBusTopic("Topic1");
topic1.AddServiceBusSubscription("Subscription1");
topic1.AddServiceBusSubscription("Subscription2");

// Add the PicoBusX web application  
builder
    .AddProject<Projects.PicoBusX_Web>("PicoBusX")
    .WithReference(serviceBus)
    .WithEnvironment("SERVICEBUS_ADMINURI", serviceBus.GetEndpoint("emulatorhealth"))
    // Pass pre-created entity names so ExplorerService can fall back to querying them individually
    // when the emulator doesn't support listing operations (GetQueuesAsync/GetTopicsAsync)
    .WithEnvironment("SERVICEBUS_KNOWN_QUEUES", "Queue1,Queue2")
    .WithEnvironment("SERVICEBUS_KNOWN_TOPICS", "Topic1")
    .WithEnvironment("SERVICEBUS_KNOWN_SUBSCRIPTIONS", "Topic1:Subscription1,Topic1:Subscription2")
    .WaitFor(serviceBus);

builder.Build().Run();

