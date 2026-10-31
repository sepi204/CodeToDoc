using System.Text.Json;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Deterministic, offline LLM provider used by automated tests.
/// It inspects the validated structured payload (never raw source) and returns
/// a valid structured response that mirrors the IDs present in the request.
/// </summary>
public sealed class FakeLlmClient : ILlmProvider
{
    private const string OpenMarker = "<verified_input_skeleton>";
    private const string CloseMarker = "</verified_input_skeleton>";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Queue<Func<LlmProviderResponse>> _scriptedResponses = new();
    private readonly int _inputTokensPerRequest;
    private readonly int _outputTokensPerRequest;

    public int InvocationCount { get; private set; }

    public FakeLlmClient(int inputTokensPerRequest = 120, int outputTokensPerRequest = 40)
    {
        _inputTokensPerRequest = inputTokensPerRequest;
        _outputTokensPerRequest = outputTokensPerRequest;
    }

    /// <summary>Queues a scripted response (e.g. a failure) to be returned on the next call.</summary>
    public void ScriptNext(Func<LlmProviderResponse> response) => _scriptedResponses.Enqueue(response);

    public Task<LlmProviderResponse> GenerateAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;

        if (_scriptedResponses.Count > 0)
        {
            return Task.FromResult(_scriptedResponses.Dequeue()());
        }

        var json = ExtractPayloadJson(request.UserPayload);
        var payload = string.IsNullOrWhiteSpace(json)
            ? new SynthesisPayload()
            : JsonSerializer.Deserialize<SynthesisPayload>(json, JsonOptions) ?? new SynthesisPayload();

        var content = request.DocumentType switch
        {
            "technical" => JsonSerializer.Serialize(BuildTechnical(payload), JsonOptions),
            "userGuide" => JsonSerializer.Serialize(BuildUserGuide(payload), JsonOptions),
            _ => "{}",
        };

        return Task.FromResult(new LlmProviderResponse
        {
            IsSuccess = true,
            Content = content,
            Usage = new LlmUsage
            {
                InputTokens = _inputTokensPerRequest,
                OutputTokens = _outputTokensPerRequest,
                TokenCountKind = TokenCountKind.ProviderReported,
            },
        });
    }

    private static TechnicalModuleResponse BuildTechnical(SynthesisPayload payload)
    {
        var endpointIds = payload.Endpoints.Select(e => e.EndpointId).ToList();

        var response = new TechnicalModuleResponse
        {
            ModuleId = payload.ModuleId,
            Title = DeriveTitle(payload.ModuleId),
            Overview = new List<SynthesisStatement>
            {
                new() { Text = $"This module handles {payload.Endpoints.Count} endpoint(s).", Certainty = "derived", EvidenceIds = endpointIds },
            },
            Technologies = new List<SynthesisStatement>
            {
                new() { Text = payload.Summary ?? "Technology stack was detected.", Certainty = "explicit" },
            },
        };

        if (payload.Endpoints.Any(e => e.AuthRequired))
        {
            response.Authentication.Add(new SynthesisStatement
            {
                Text = "The module requires authentication for protected endpoints.",
                Certainty = "explicit",
                EvidenceIds = payload.Endpoints.Where(e => e.AuthRequired).Select(e => e.EndpointId).ToList(),
            });
        }

        response.Endpoints = payload.Endpoints
            .Select(e => new TechnicalEndpointSection
            {
                EndpointId = e.EndpointId,
                Summary = $"Handles {e.Method} {e.Path}.",
                Certainty = "explicit",
            })
            .ToList();

        return response;
    }

    private static UserGuideModuleResponse BuildUserGuide(SynthesisPayload payload)
    {
        var firstPage = payload.Pages.FirstOrDefault();

        var response = new UserGuideModuleResponse
        {
            ModuleId = payload.ModuleId,
            Title = firstPage?.Title ?? DeriveTitle(payload.ModuleId),
            Pages = payload.Pages.Select(BuildPage).ToList(),
        };

        return response;
    }

    private static UserGuidePageSection BuildPage(PayloadPage page)
    {
        return new UserGuidePageSection
        {
            PageId = page.PageId,
            DisplayTitle = page.Title ?? page.Route,
            Forms = page.Forms.Select(form => new UserGuideFormSection
            {
                FormId = form.FormId,
                Steps = form.Fields.Select((field, index) => new UserGuideStep
                {
                    Order = index + 1,
                    Text = $"فیلد «{field.Label ?? field.Name}» را تکمیل کنید.",
                    EvidenceIds = new List<string>(),
                }).ToList(),
                Outcomes = form.SubmitActions.Select(action => new SynthesisStatement
                {
                    Text = BuildOutcome(action),
                    Certainty = action.PersistenceKind == "evidence" ? "explicit" : "unknown",
                }).ToList(),
            }).ToList(),
            Actions = page.Actions.Select(action => new UserGuideActionSection
            {
                ActionId = action.ActionId,
                Label = action.Label,
                Outcome = BuildOutcome(action),
                Certainty = action.PersistenceKind == "evidence" ? "explicit" : "unknown",
            }).ToList(),
            Tables = page.Tables.Select(table => new UserGuideTableSection
            {
                TableId = table.TableId,
                Columns = table.Columns,
            }).ToList(),
        };
    }

    private static string BuildOutcome(PayloadAction action)
    {
        if (action.PersistenceKind == "evidence")
        {
            return $"با انتخاب «{action.Label}»، اطلاعات واردشده ثبت می‌شود.";
        }

        if (action.NavigationTarget is not null)
        {
            return $"با انتخاب «{action.Label}»، به بخش بعدی منتقل می‌شوید.";
        }

        return $"برای ادامه، دکمه «{action.Label}» را انتخاب کنید.";
    }

    private static string ExtractPayloadJson(string userPayload)
    {
        var open = userPayload.IndexOf(OpenMarker, StringComparison.Ordinal);
        var close = userPayload.IndexOf(CloseMarker, StringComparison.Ordinal);
        if (open < 0 || close < 0 || close <= open)
        {
            return userPayload;
        }

        return userPayload[(open + OpenMarker.Length)..close].Trim();
    }

    private static string DeriveTitle(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return "Module";
        }

        var words = moduleId.Split('-', '_')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(' ', words);
    }
}
