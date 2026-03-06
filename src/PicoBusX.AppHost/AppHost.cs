using Aspire.Hosting.Azure;
using PicoBusX.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Service Bus with emulator support
// Expose port 5300 for admin/management operations using WithEndpoint
var serviceBus = builder
    .AddAzureServiceBus("serviceBus")
    .RunAsEmulator(resourceBuilder =>
        resourceBuilder.WithImageTag("latest").WithLifetime(ContainerLifetime.Persistent));

SeedExplorerEntities(serviceBus);

// Add the PicoBusX web application  
var picoBusX = builder
    .AddProject<Projects.PicoBusX_Web>("PicoBusX")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

serviceBus.WithAdminConnectionStringEnvironment(picoBusX);


builder.Build().Run();

static void SeedExplorerEntities(IResourceBuilder<AzureServiceBusResource> serviceBus)
{
    serviceBus.AddServiceBusQueue("orders");
    serviceBus.AddServiceBusQueue("billing");
    serviceBus.AddServiceBusQueue("retries");

    var ordersTopic = serviceBus.AddServiceBusTopic("orders-topic");
    ordersTopic.AddServiceBusSubscription("order-created");
    ordersTopic.AddServiceBusSubscription("order-cancelled");

    var inventoryTopic = serviceBus.AddServiceBusTopic("inventory-topic");
    inventoryTopic.AddServiceBusSubscription("restock");
    inventoryTopic.AddServiceBusSubscription("warehouse");
}
