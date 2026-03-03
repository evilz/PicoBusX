using PicoBusX.Web.Components;
using PicoBusX.Web.Options;
using PicoBusX.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Options: ASB_CONNECTION_STRING env var takes priority over appsettings ServiceBus:ConnectionString
// Environment variables are automatically loaded by WebApplication.CreateBuilder
builder.Services.Configure<ServiceBusConnectionOptions>(options =>
{
    options.ConnectionString = builder.Configuration["ASB_CONNECTION_STRING"]
        ?? builder.Configuration["ServiceBus:ConnectionString"];
    options.TransportType = builder.Configuration["ASB_TRANSPORT_TYPE"] ?? "AmqpTcp";
    if (int.TryParse(builder.Configuration["ASB_ENTITY_MAX_PEEK"], out var maxPeek))
        options.EntityMaxPeek = maxPeek;
    else
        options.EntityMaxPeek = 10;
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
