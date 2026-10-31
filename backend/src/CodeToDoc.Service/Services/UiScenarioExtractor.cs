using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class UiScenarioExtractor : IUiScenarioExtractor
{
    public List<UiScenario> Extract(ParsedUiSource source, IReadOnlyList<UiAction> pageActions)
    {
        var scenarios = new List<UiScenario>();
        var postFetches = source.Fetches
            .Where(f => string.Equals(f.Method, "POST", StringComparison.OrdinalIgnoreCase) || f.Method is null)
            .ToList();

        foreach (var action in pageActions)
        {
            switch (action.Kind)
            {
                case ActionKind.SubmitForm:
                    var submit = new UiScenario
                    {
                        Id = $"scenario_{Sanitize(action.Label ?? action.Id)}",
                        TriggerElementId = action.Id,
                        Kind = ActionKind.SubmitForm,
                        HandlerExpression = action.HandlerExpression,
                    };

                    foreach (var fetch in postFetches)
                    {
                        submit.ApiReferences.Add(new UiApiReference
                        {
                            Method = fetch.Method ?? "GET",
                            Url = fetch.Url,
                            UrlExpression = fetch.UrlExpression,
                        });
                    }

                    scenarios.Add(submit);
                    break;

                case ActionKind.Navigate:
                    scenarios.Add(new UiScenario
                    {
                        Id = $"scenario_{Sanitize(action.Label ?? action.Id)}",
                        TriggerElementId = action.Id,
                        Kind = ActionKind.Navigate,
                        NavigationTarget = action.NavigationTarget,
                    });
                    break;

                default:
                    if (action.HandlerExpression is not null)
                    {
                        scenarios.Add(new UiScenario
                        {
                            Id = $"scenario_{Sanitize(action.Label ?? action.Id)}",
                            TriggerElementId = action.Id,
                            Kind = ActionKind.Unknown,
                            HandlerExpression = action.HandlerExpression,
                            UnresolvedBehavior = "Handler could not be statically resolved to a supported interaction.",
                        });
                    }
                    break;
            }
        }

        return scenarios;
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        var result = builder.ToString().Trim('_');
        return result.Length == 0 ? "anon" : result;
    }
}
