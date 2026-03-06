using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Options;
using PicoBusX.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire Azure Messaging ServiceBus client integration
// This automatically configures ServiceBusClient from the connection string
builder.AddAzureServiceBusClient("serviceBus");


builder.Services.AddOptions<ServiceBusConnectionOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusConnectionOptions.SectionName))
    .Bind(builder.Configuration)      
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Services
builder.Services.AddSingleton<ServiceBusClientFactory>();
builder.Services.AddScoped<ExplorerService>();
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
