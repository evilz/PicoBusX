using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Options;
using PicoBusX.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire Azure Messaging ServiceBus client integration when a connection string is pre-configured.
// When running without Aspire (e.g., standalone Docker), the user configures the connection via the
// Settings page at runtime, so we skip this registration to avoid startup failures.
if (builder.Configuration.GetConnectionString("serviceBus") is not null)
{
    builder.AddAzureServiceBusClient("serviceBus");
}

builder.Services.AddOptions<ServiceBusConnectionOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusConnectionOptions.SectionName))
    .Bind(builder.Configuration)
    .ValidateDataAnnotations();

// Services
builder.Services.AddSingleton<IConnectionSettingsStore, ConnectionSettingsStore>();
builder.Services.AddSingleton<ServiceBusClientFactory>();
builder.Services.AddScoped<IExplorerService, ExplorerService>();
builder.Services.AddScoped<MessageSenderService>();
builder.Services.AddScoped<MessageBrowserService>();
builder.Services.AddScoped<EntityManagementService>();

// Add Fluent UI Blazor services
builder.Services.AddFluentUIComponents();

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
