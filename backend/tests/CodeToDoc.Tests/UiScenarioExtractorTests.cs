using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class UiScenarioExtractorTests
{
    private readonly UiScenarioExtractor _extractor = new();

    [Fact]
    public void SubmitFormScenarioWithApiReference()
    {
        var source = new ParsedUiSource();
        source.Fetches.Add(new ParsedFetch { Method = "POST", Url = "/api/customers", Line = 1 });

        var action = new UiAction { Id = "action_submit", Label = "ثبت نهایی", Kind = ActionKind.SubmitForm, HandlerExpression = "handleSubmit" };

        var scenarios = _extractor.Extract(source, new[] { action });

        var scenario = Assert.Single(scenarios);
        Assert.Equal(ActionKind.SubmitForm, scenario.Kind);
        Assert.Equal("action_submit", scenario.TriggerElementId);
        Assert.Equal("handleSubmit", scenario.HandlerExpression);

        var api = Assert.Single(scenario.ApiReferences);
        Assert.Equal("POST", api.Method);
        Assert.Equal("/api/customers", api.Url);
    }

    [Fact]
    public void ExplicitNavigationScenario()
    {
        var action = new UiAction { Id = "action_link", Label = "مشتری جدید", Kind = ActionKind.Navigate, NavigationTarget = "/customers/new" };

        var scenarios = _extractor.Extract(new ParsedUiSource(), new[] { action });

        var scenario = Assert.Single(scenarios);
        Assert.Equal(ActionKind.Navigate, scenario.Kind);
        Assert.Equal("/customers/new", scenario.NavigationTarget);
    }

    [Fact]
    public void UnresolvedHandlerDoesNotInventOutcome()
    {
        var action = new UiAction { Id = "action_x", Label = "Do", Kind = ActionKind.Unknown, HandlerExpression = "someHandler" };

        var scenarios = _extractor.Extract(new ParsedUiSource(), new[] { action });

        var scenario = Assert.Single(scenarios);
        Assert.Equal(ActionKind.Unknown, scenario.Kind);
        Assert.NotNull(scenario.UnresolvedBehavior);
        Assert.Empty(scenario.ApiReferences);
    }
}
