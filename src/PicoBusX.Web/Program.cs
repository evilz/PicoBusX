using PicoBusX.Web.Components;
using PicoBusX.Web.Options;
using PicoBusX.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire Azure Messaging ServiceBus client integration
// This automatically configures ServiceBusClient from the connection string
builder.AddAzureServiceBusClient("serviceBus");

// Options: ConnectionStrings:serviceBus (Aspire) > ASB_CONNECTION_STRING (env var) > ServiceBus:ConnectionString (appsettings)
builder.Services.Configure<ServiceBusConnectionOptions>(options =>
{
    options.ConnectionString = builder.Configuration["ConnectionStrings:serviceBus"]
        ?? builder.Configuration["ASB_CONNECTION_STRING"]
        ?? builder.Configuration["ServiceBus:ConnectionString"];
    options.AdminUri = builder.Configuration["SERVICEBUS_ADMINURI"];
    options.TransportType = builder.Configuration["ASB_TRANSPORT_TYPE"]
        ?? builder.Configuration["ServiceBus:TransportType"]
        ?? "AmqpTcp";
    if (int.TryParse(builder.Configuration["ASB_ENTITY_MAX_PEEK"] ?? builder.Configuration["ServiceBus:EntityMaxPeek"], out var maxPeek))
        options.EntityMaxPeek = maxPeek;
    else
        options.EntityMaxPeek = 10;

    // Known entities from AppHost (for emulator fallback when listing operations aren't supported)
    options.KnownQueues = builder.Configuration["SERVICEBUS_KNOWN_QUEUES"];
    options.KnownTopics = builder.Configuration["SERVICEBUS_KNOWN_TOPICS"];
    options.KnownSubscriptions = builder.Configuration["SERVICEBUS_KNOWN_SUBSCRIPTIONS"];
});

// Services
builder.Services.AddSingleton<ServiceBusClientFactory>();
builder.Services.AddScoped<ExplorerService>();
builder.Services.AddScoped<MessageSenderService>();
builder.Services.AddScoped<MessageBrowserService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
