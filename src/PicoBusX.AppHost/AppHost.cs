using PicoBusX.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Service Bus with emulator support
// Expose port 5300 for admin/management operations using WithEndpoint
var serviceBus = builder
    .AddAzureServiceBus("serviceBus")
    .RunAsEmulator(resourceBuilder =>
        resourceBuilder.WithImageTag("latest").WithLifetime(ContainerLifetime.Persistent));

// Pre-create default entities for exploration and testing
serviceBus.AddServiceBusQueue("Queue1");
serviceBus.AddServiceBusQueue("Queue2");

var topic1 = serviceBus.AddServiceBusTopic("Topic1");
topic1.AddServiceBusSubscription("Subscription1");
topic1.AddServiceBusSubscription("Subscription2");


// Add the PicoBusX web application  
var picoBusX = builder
    .AddProject<Projects.PicoBusX_Web>("PicoBusX")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

serviceBus.WithAdminConnectionStringEnvironment(picoBusX);


builder.Build().Run();