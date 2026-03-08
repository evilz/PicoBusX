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

        var factoryOptions = MsOptions.Create(new ServiceBusConnectionOptions());
        Services.AddSingleton<IOptions<ServiceBusConnectionOptions>>(factoryOptions);
        Services.AddSingleton(new ServiceBusClientFactory(factoryOptions, NullLogger<ServiceBusClientFactory>.Instance));
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Select a queue, topic, or subscription"));
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

    private static Task InvokePrivateMethod<TComponent>(TComponent instance, string methodName, params object[] args)
    {
        var method = typeof(TComponent).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {typeof(TComponent).Name}.");
        var result = method.Invoke(instance, args);
        return result is Task task ? task : Task.CompletedTask;
    }
}
