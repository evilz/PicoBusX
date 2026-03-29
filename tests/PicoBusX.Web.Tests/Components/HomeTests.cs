using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components.Pages;
using PicoBusX.Web.Models;
using PicoBusX.Web.Options;
using PicoBusX.Web.Services;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace PicoBusX.Web.Tests.Components;

public class HomeTests : TestContext
{
    private sealed class InMemoryConnectionSettingsStore : IConnectionSettingsStore
    {
        private ServiceBusConnectionOptions? _settings;

        public bool HasRuntimeSettings => _settings is not null;

        public ServiceBusConnectionOptions? GetRuntimeSettings() => _settings;

        public Task SaveAsync(ServiceBusConnectionOptions settings)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _settings = null;
            return Task.CompletedTask;
        }
    }

    private class StubExplorerService(ExplorerLoadResult result) : IExplorerService
    {
        public Task<ExplorerLoadResult> LoadAsync(CancellationToken ct = default) => Task.FromResult(result);
        public Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default) => Task.FromResult(result.Queues);
        public Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default) => Task.FromResult(result.Topics);
    }

    private void SetupServices(ExplorerLoadResult? loadResult = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddLogging();

        var factoryOptions = MsOptions.Create(new ServiceBusConnectionOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dummy"
        });
        Services.AddSingleton<IOptions<ServiceBusConnectionOptions>>(factoryOptions);
        Services.AddSingleton<IConnectionSettingsStore, InMemoryConnectionSettingsStore>();
        Services.AddSingleton(sp => new ServiceBusClientFactory(
            factoryOptions,
            sp.GetRequiredService<IConnectionSettingsStore>(),
            NullLogger<ServiceBusClientFactory>.Instance));
        Services.AddScoped<IExplorerService>(_ => new StubExplorerService(loadResult ?? new ExplorerLoadResult()));
        Services.AddScoped<MessageSenderService>();
        Services.AddScoped<MessageBrowserService>();
        Services.AddScoped<EntityManagementService>();
    }

    [Fact]
    public void QueryParameters_WithValidQueueTypeAndName_HydratesSelectedEntity()
    {
        SetupServices(new ExplorerLoadResult
        {
            Queues = [new QueueInfo { Name = "orders" }],
            Topics = []
        });

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=orders&type=Queue");

        var cut = RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("orders"));
    }

    [Fact]
    public void QueryParameters_WithUnrecognizedEntityType_LeavesSelectedEntityNull()
    {
        SetupServices();

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=my-queue&type=UnknownType");

        var cut = RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().NotContain("my-queue</h2>");
            cut.Markup.Should().MatchRegex("Select a queue, topic, or subscription|No Azure Service Bus connection is configured");
        });
    }

    [Fact]
    public async Task HandleEntitySelected_NavigatesToUrlWithNameAndTypeQueryParams()
    {
        SetupServices();

        var cut = RenderComponent<Home>();

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        var entity = new BusEntity { Type = BusEntityType.Queue, Name = "billing" };

        await cut.InvokeAsync(() => InvokePrivateMethod(cut.Instance, "HandleEntitySelected", entity));

        navManager.Uri.Should().Contain("name=billing");
        navManager.Uri.Should().Contain("type=Queue");
    }

    [Fact]
    public async Task HandleEntitySelected_ForSubscription_IncludesTopicNameInQueryParams()
    {
        SetupServices();

        var cut = RenderComponent<Home>();

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        var entity = new BusEntity { Type = BusEntityType.Subscription, Name = "order-created", TopicName = "orders-topic" };

        await cut.InvokeAsync(() => InvokePrivateMethod(cut.Instance, "HandleEntitySelected", entity));

        navManager.Uri.Should().Contain("name=order-created");
        navManager.Uri.Should().Contain("type=Subscription");
        navManager.Uri.Should().Contain("topicName=orders-topic");
    }

    [Fact]
    public void IsSessionEnabled_SessionQueue_ReturnsTrue()
    {
        SetupServices(new ExplorerLoadResult
        {
            Queues = [new QueueInfo { Name = "orders-session", RequiresSession = true }],
            Topics = []
        });

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=orders-session&type=Queue");

        var cut = RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("orders-session"));

        var result = InvokePrivateBoolMethod(cut.Instance, "IsSessionEnabled");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSessionEnabled_NonSessionQueue_ReturnsFalse()
    {
        SetupServices(new ExplorerLoadResult
        {
            Queues = [new QueueInfo { Name = "orders", RequiresSession = false }],
            Topics = []
        });

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=orders&type=Queue");

        var cut = RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("orders"));

        var result = InvokePrivateBoolMethod(cut.Instance, "IsSessionEnabled");
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSessionEnabled_SessionSubscription_ReturnsTrue()
    {
        var sub = new SubscriptionInfo { TopicName = "orders-topic", Name = "order-created", RequiresSession = true };
        var topic = new TopicInfo { Name = "orders-topic", Subscriptions = [sub] };

        SetupServices(new ExplorerLoadResult
        {
            Queues = [],
            Topics = [topic]
        });

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=order-created&type=Subscription&topicName=orders-topic");

        var cut = RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("order-created"));

        var result = InvokePrivateBoolMethod(cut.Instance, "IsSessionEnabled");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSessionEnabled_NonSessionSubscription_ReturnsFalse()
    {
        var sub = new SubscriptionInfo { TopicName = "orders-topic", Name = "order-created", RequiresSession = false };
        var topic = new TopicInfo { Name = "orders-topic", Subscriptions = [sub] };

        SetupServices(new ExplorerLoadResult
        {
            Queues = [],
            Topics = [topic]
        });

        var navManager = Services.GetRequiredService<FakeNavigationManager>();
        navManager.NavigateTo("http://localhost/?name=order-created&type=Subscription&topicName=orders-topic");

        var cut = RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("order-created"));

        var result = InvokePrivateBoolMethod(cut.Instance, "IsSessionEnabled");
        result.Should().BeFalse();
    }

    private static Task InvokePrivateMethod<TComponent>(TComponent instance, string methodName, params object[] args)
    {
        var result = InvokePrivateMethodCore<TComponent>(instance, methodName, args);
        return result is Task task ? task : Task.CompletedTask;
    }

    private static bool InvokePrivateBoolMethod<TComponent>(TComponent instance, string methodName)
        => (bool)InvokePrivateMethodCore<TComponent>(instance, methodName, [])!;

    private static object? InvokePrivateMethodCore<TComponent>(TComponent instance, string methodName, object?[] args)
    {
        var method = typeof(TComponent).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {typeof(TComponent).Name}.");
        return method.Invoke(instance, args);
    }
}
