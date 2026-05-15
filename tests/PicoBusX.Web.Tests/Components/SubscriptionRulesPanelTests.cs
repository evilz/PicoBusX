using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Tests.Components;

public class SubscriptionRulesPanelTests : TestContext
{
    public SubscriptionRulesPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddOptions();
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void NoRules_RendersEmptyStateMessage()
    {
        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, []));

        cut.Markup.Should().Contain("No rules defined");
    }

    [Fact]
    public void WithRules_RendersRulesTable()
    {
        var rules = new List<RuleInfo>
        {
            new() { Name = "sql-rule", FilterType = "SQL", FilterExpression = "sys.Label = &#x27;order&#x27;", ActionExpression = null },
            new() { Name = "true-rule", FilterType = "True", FilterExpression = "1=1", ActionExpression = null }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules));

        cut.Markup.Should().Contain("sql-rule");
        cut.Markup.Should().Contain("SQL");
        cut.Markup.Should().Contain("true-rule");
        cut.Markup.Should().Contain("True");
        cut.Markup.Should().Contain("1=1");
    }

    [Fact]
    public void WithRules_RendersRuleNames()
    {
        var rules = new List<RuleInfo>
        {
            new() { Name = "sql-rule", FilterType = "SQL", FilterExpression = "1=1" },
            new() { Name = "true-rule", FilterType = "True", FilterExpression = "1=1" }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules));

        cut.Markup.Should().Contain("sql-rule");
        cut.Markup.Should().Contain("true-rule");
    }

    [Fact]
    public void WithRules_RendersDeleteButton()
    {
        var rules = new List<RuleInfo>
        {
            new() { Name = "rule-one", FilterType = "SQL", FilterExpression = "1=1" }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules));

        cut.Markup.Should().Contain("Delete rule");
    }

    [Fact]
    public void ShowAddForm_SetsShowAddFormToTrue()
    {
        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, []));

        cut.Instance.GetType()
            .GetField("_showAddForm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, true);
        cut.Render();

        cut.Markup.Should().Contain("Rule Name");
        cut.Markup.Should().Contain("Filter Type");
    }

    [Fact]
    public async Task HandleCreateAsync_WithEmptyName_SetsAddError()
    {
        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, []));

        await cut.InvokeAsync(async () =>
        {
            var method = cut.Instance.GetType()
                .GetMethod("HandleCreateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(cut.Instance, null)!;
        });

        var addError = (string?)cut.Instance.GetType()
            .GetField("_addError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cut.Instance);
        addError.Should().Contain("Rule name is required");
    }

    [Fact]
    public async Task HandleCreateAsync_SqlWithEmptyExpression_SetsAddError()
    {
        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, []));

        cut.Instance.GetType()
            .GetField("_newRuleName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, "my-rule");

        await cut.InvokeAsync(async () =>
        {
            var method = cut.Instance.GetType()
                .GetMethod("HandleCreateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(cut.Instance, null)!;
        });

        var addError = (string?)cut.Instance.GetType()
            .GetField("_addError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cut.Instance);
        addError.Should().Contain("SQL filter expression is required");
    }

    [Fact]
    public async Task HandleCreateAsync_ValidSqlRule_InvokesOnCreateRule()
    {
        (string name, string filterType, string? sqlExpression, string? actionExpression)? received = null;

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, [])
            .Add(p => p.OnCreateRule, EventCallback.Factory.Create<(string, string, string?, string?)>(
                this, args => received = args)));

        cut.Instance.GetType()
            .GetField("_newRuleName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, "my-rule");
        cut.Instance.GetType()
            .GetField("_newFilterType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, "sql");
        cut.Instance.GetType()
            .GetField("_newSqlExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(cut.Instance, "sys.Label = 'order'");

        await cut.InvokeAsync(async () =>
        {
            var method = cut.Instance.GetType()
                .GetMethod("HandleCreateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(cut.Instance, null)!;
        });

        received.Should().NotBeNull();
        received!.Value.name.Should().Be("my-rule");
        received!.Value.filterType.Should().Be("sql");
        received!.Value.sqlExpression.Should().Be("sys.Label = 'order'");
    }

    [Fact]
    public async Task HandleDeleteAsync_InvokesOnDeleteRule()
    {
        string? deletedRule = null;

        var rules = new List<RuleInfo>
        {
            new() { Name = "to-delete", FilterType = "True", FilterExpression = "1=1" }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules)
            .Add(p => p.OnDeleteRule, EventCallback.Factory.Create<string>(
                this, name => deletedRule = name)));

        await cut.InvokeAsync(async () =>
        {
            var method = cut.Instance.GetType()
                .GetMethod("HandleDeleteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(cut.Instance, ["to-delete"])!;
        });

        deletedRule.Should().Be("to-delete");
    }

    [Fact]
    public void WithRules_RendersActionExpression()
    {
        var rules = new List<RuleInfo>
        {
            new() { Name = "action-rule", FilterType = "SQL", FilterExpression = "1=1", ActionExpression = "SET sys.Label = 'done'" }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules));

        cut.Markup.Should().Contain("SET sys.Label");
    }

    [Fact]
    public void WithRules_EmptyActionExpression_RendersEmDash()
    {
        var rules = new List<RuleInfo>
        {
            new() { Name = "no-action", FilterType = "SQL", FilterExpression = "1=1", ActionExpression = null }
        };

        var cut = RenderComponent<SubscriptionRulesPanel>(parameters => parameters
            .Add(p => p.Rules, rules));

        cut.Markup.Should().Contain("—");
    }
}
