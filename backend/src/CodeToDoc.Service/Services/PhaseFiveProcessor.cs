using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class PhaseFiveProcessor : IPhaseFiveProcessor
{
    private const string TechnicalArtifact = "technical_document.md";
    private const string UserGuideArtifact = "user_guide.md";
    private const string ManifestArtifact = "synthesis_manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ILlmProvider _provider;
    private readonly IPromptBuilder _promptBuilder;
    private readonly LlmOptions _options;
    private readonly ILogger<PhaseFiveProcessor> _logger;

    public PhaseFiveProcessor(
        ILlmProvider provider,
        IPromptBuilder promptBuilder,
        IOptions<LlmOptions> options,
        ILogger<PhaseFiveProcessor> logger)
    {
        _provider = provider;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SynthesisManifest> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var manifest = new SynthesisManifest
        {
            JobId = workspace.JobId,
            GeneratedAtUtc = DateTime.UtcNow,
            Status = SynthesisStatus.Complete,
        };

        var technical = Load<TechnicalSkeleton>(workspace.JobRootPath, "technical_skeleton.json");
        var ui = Load<UiSkeleton>(workspace.JobRootPath, "ui_skeleton.json");
        var correlation = Load<CorrelationSkeleton>(workspace.JobRootPath, "correlation_skeleton.json");

        // Technical document.
        if (technical is null || technical.Endpoints.Count == 0)
        {
            manifest.Documents.Add(new SynthesisDocumentEntry
            {
                DocumentType = "technical",
                Status = SynthesisStatus.NotApplicable,
                Artifact = TechnicalArtifact,
                Language = _options.TechnicalDocumentLanguage,
                Model = _options.Model,
            });
        }
        else
        {
            manifest.Documents.Add(await SynthesizeTechnicalAsync(workspace, technical, cancellationToken));
        }

        // User guide.
        if (ui is null || ui.Pages.Count == 0)
        {
            manifest.Documents.Add(new SynthesisDocumentEntry
            {
                DocumentType = "userGuide",
                Status = SynthesisStatus.NotApplicable,
                Artifact = UserGuideArtifact,
                Language = _options.UserGuideLanguage,
                Model = _options.Model,
            });
        }
        else
        {
            manifest.Documents.Add(await SynthesizeUserGuideAsync(workspace, ui, correlation, cancellationToken));
        }

        manifest.Status = Aggregate(manifest.Documents);
        manifest.Diagnostics = manifest.Documents.SelectMany(d => d.Diagnostics).ToList();

        await WriteAtomicAsync(workspace.JobRootPath, ManifestArtifact,
            JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

        _logger.LogInformation("Phase 5 completed for job {JobId} (status {Status}).", workspace.JobId, manifest.Status);

        return manifest;
    }

    // ---- technical synthesis ----

    private async Task<SynthesisDocumentEntry> SynthesizeTechnicalAsync(
        JobWorkspace workspace, TechnicalSkeleton technical, CancellationToken cancellationToken)
    {
        var entry = new SynthesisDocumentEntry
        {
            DocumentType = "technical",
            Artifact = TechnicalArtifact,
            Language = _options.TechnicalDocumentLanguage,
            Model = _options.Model,
            TemplateVersions = { PromptBuilder.TechnicalSystemVersion, PromptBuilder.TechnicalReduceVersion },
            Status = SynthesisStatus.Complete,
        };

        var partitions = PartitionTechnical(technical, workspace.JobId);
        var modules = new List<TechnicalModuleResponse>();
        var inputBytes = 0L;

        foreach (var partition in partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prompt = _promptBuilder.BuildTechnical(partition.Payload);
            inputBytes += Encoding.UTF8.GetByteCount(prompt.UserPayload);
            entry.InputFingerprint = Hash(prompt.UserPayload);

            var request = new LlmProviderRequest
            {
                Model = _options.Model,
                SystemInstruction = prompt.SystemInstruction,
                UserPayload = prompt.UserPayload,
                DocumentType = "technical",
                ModuleId = partition.ModuleId,
                TemplateVersion = prompt.TemplateVersion,
            };

            var response = await CallWithRetryAsync(request, entry, cancellationToken);

            if (response.IsSuccess && TryParseTechnical(response.Content, out var module))
            {
                modules.Add(module!);
            }
            else
            {
                entry.Status = SynthesisStatus.Partial;
                entry.Diagnostics.Add(new UiDiagnostic
                {
                    Code = "synth.moduleFailed",
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Technical module '{partition.ModuleId}' failed or produced an invalid response.",
                });
            }

            entry.Usage.InputTokens += response.Usage.InputTokens;
            entry.Usage.OutputTokens += response.Usage.OutputTokens;
            entry.Usage.TokenCountKind = response.Usage.TokenCountKind;
        }

        entry.ModuleCount = partitions.Count;

        var merged = MergeTechnical(modules);
        var fullPayload = BuildTechnicalPayload(technical, workspace.JobId);
        var markdown = MarkdownRenderer.RenderTechnical(fullPayload, merged, entry.Status == SynthesisStatus.Partial);
        entry.InputFingerprint = Hash(JsonSerializer.Serialize(fullPayload, JsonOptions));

        await WriteAtomicAsync(workspace.JobRootPath, TechnicalArtifact, markdown, cancellationToken);

        return entry;
    }

    // ---- user guide synthesis ----

    private async Task<SynthesisDocumentEntry> SynthesizeUserGuideAsync(
        JobWorkspace workspace, UiSkeleton ui, CorrelationSkeleton? correlation, CancellationToken cancellationToken)
    {
        var entry = new SynthesisDocumentEntry
        {
            DocumentType = "userGuide",
            Artifact = UserGuideArtifact,
            Language = _options.UserGuideLanguage,
            Model = _options.Model,
            TemplateVersions = { PromptBuilder.UserGuideSystemVersion, PromptBuilder.UserGuideReduceVersion },
            Status = SynthesisStatus.Complete,
        };

        var partitions = PartitionUserGuide(ui, correlation, workspace.JobId);
        var modules = new List<UserGuideModuleResponse>();

        foreach (var partition in partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prompt = _promptBuilder.BuildUserGuide(partition.Payload);
            var request = new LlmProviderRequest
            {
                Model = _options.Model,
                SystemInstruction = prompt.SystemInstruction,
                UserPayload = prompt.UserPayload,
                DocumentType = "userGuide",
                ModuleId = partition.ModuleId,
                TemplateVersion = prompt.TemplateVersion,
            };

            var response = await CallWithRetryAsync(request, entry, cancellationToken);

            if (response.IsSuccess && TryParseUserGuide(response.Content, out var module))
            {
                modules.Add(module!);
            }
            else
            {
                entry.Status = SynthesisStatus.Partial;
                entry.Diagnostics.Add(new UiDiagnostic
                {
                    Code = "synth.moduleFailed",
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"User-guide module '{partition.ModuleId}' failed or produced an invalid response.",
                });
            }

            entry.Usage.InputTokens += response.Usage.InputTokens;
            entry.Usage.OutputTokens += response.Usage.OutputTokens;
            entry.Usage.TokenCountKind = response.Usage.TokenCountKind;
        }

        entry.ModuleCount = partitions.Count;

        var merged = MergeUserGuide(modules);
        var fullPayload = BuildUserGuidePayload(ui, correlation, workspace.JobId);
        var markdown = MarkdownRenderer.RenderUserGuide(fullPayload, merged, entry.Status == SynthesisStatus.Partial);
        entry.InputFingerprint = Hash(JsonSerializer.Serialize(fullPayload, JsonOptions));

        await WriteAtomicAsync(workspace.JobRootPath, UserGuideArtifact, markdown, cancellationToken);

        return entry;
    }

    // ---- partitioning ----

    private List<SynthesisPartition> PartitionTechnical(TechnicalSkeleton technical, string jobId)
    {
        var indexed = technical.Endpoints.Select((e, i) => (Endpoint: e, Id: $"ep_{i}")).ToList();

        return indexed
            .GroupBy(x => ModuleKey(x.Endpoint.ControllerName))
            .Select(group =>
            {
                var endpoints = group.ToList();
                return new SynthesisPartition
                {
                    ModuleId = group.Key,
                    Payload = new SynthesisPayload
                    {
                        JobId = jobId,
                        DocumentType = "technical",
                        ModuleId = group.Key,
                        Summary = BuildTechnicalSummary(technical),
                        Endpoints = endpoints.Select(x => BuildPayloadEndpoint(x.Endpoint, x.Id)).ToList(),
                    },
                };
            })
            .OrderBy(p => p.ModuleId, StringComparer.Ordinal)
            .ToList();
    }

    private static SynthesisPayload BuildTechnicalPayload(TechnicalSkeleton technical, string jobId)
    {
        var indexed = technical.Endpoints.Select((e, i) => (Endpoint: e, Id: $"ep_{i}")).ToList();
        return new SynthesisPayload
        {
            JobId = jobId,
            DocumentType = "technical",
            ModuleId = "all",
            Summary = BuildTechnicalSummary(technical),
            Endpoints = indexed.Select(x => BuildPayloadEndpoint(x.Endpoint, x.Id)).ToList(),
        };
    }

    private static PayloadEndpoint BuildPayloadEndpoint(EndpointModel endpoint, string id)
    {
        return new PayloadEndpoint
        {
            EndpointId = id,
            Method = endpoint.Method ?? string.Empty,
            Path = endpoint.Path ?? string.Empty,
            ControllerName = endpoint.ControllerName,
            AuthRequired = endpoint.AuthRequired,
            Roles = endpoint.Roles ?? new List<string>(),
            RequestFields = (endpoint.RequestDto?.Properties ?? new List<PropertyModel>())
                .Select(p => new PayloadField { Name = p.Name, Type = p.Type ?? string.Empty, IsRequired = p.IsRequired })
                .ToList(),
            ResponseFields = (endpoint.ResponseDto?.Properties ?? new List<PropertyModel>())
                .Select(p => new PayloadField { Name = p.Name, Type = p.Type ?? string.Empty })
                .ToList(),
        };
    }

    private static string BuildTechnicalSummary(TechnicalSkeleton technical)
    {
        var summary = technical.Summary;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.Framework)) parts.Add(summary.Framework);
        if (!string.IsNullOrWhiteSpace(summary.Orm)) parts.Add($"ORM: {summary.Orm}");
        if (!string.IsNullOrWhiteSpace(summary.Database)) parts.Add($"Database: {summary.Database}");
        if (!string.IsNullOrWhiteSpace(summary.Auth)) parts.Add($"Auth: {summary.Auth}");
        return string.Join(", ", parts);
    }

    private List<SynthesisPartition> PartitionUserGuide(UiSkeleton ui, CorrelationSkeleton? correlation, string jobId)
    {
        var persistenceByAction = correlation?.Correlations
            .ToDictionary(c => c.ActionId, c => c.Persistence.Kind.ToString().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>();

        return ui.Pages
            .GroupBy(p => ModuleKeyFromRoute(p.Route))
            .Select(group =>
            {
                var pages = group.ToList();
                return new SynthesisPartition
                {
                    ModuleId = group.Key,
                    Payload = new SynthesisPayload
                    {
                        JobId = jobId,
                        DocumentType = "userGuide",
                        ModuleId = group.Key,
                        Pages = pages.Select(p => BuildPayloadPage(p, persistenceByAction)).ToList(),
                    },
                };
            })
            .OrderBy(p => p.ModuleId, StringComparer.Ordinal)
            .ToList();
    }

    private static SynthesisPayload BuildUserGuidePayload(UiSkeleton ui, CorrelationSkeleton? correlation, string jobId)
    {
        var persistenceByAction = correlation?.Correlations
            .ToDictionary(c => c.ActionId, c => c.Persistence.Kind.ToString().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>();

        return new SynthesisPayload
        {
            JobId = jobId,
            DocumentType = "userGuide",
            ModuleId = "all",
            Pages = ui.Pages.Select(p => BuildPayloadPage(p, persistenceByAction)).ToList(),
        };
    }

    private static PayloadPage BuildPayloadPage(UiPage page, Dictionary<string, string> persistenceByAction)
    {
        return new PayloadPage
        {
            PageId = page.Id,
            Route = page.Route ?? string.Empty,
            Title = page.Title,
            Forms = page.Forms.Select(form => new PayloadForm
            {
                FormId = form.Id,
                Fields = form.Fields.Select(f => new PayloadField
                {
                    Name = f.Name ?? string.Empty,
                    Label = f.Label,
                    Type = f.InputType.ToString().ToLowerInvariant(),
                    IsRequired = f.Required == true,
                }).ToList(),
                SubmitActions = page.Actions
                    .Where(a => a.Kind == Enums.ActionKind.SubmitForm && a.FormId == form.Id)
                    .Select(a => BuildPayloadAction(a, persistenceByAction))
                    .ToList(),
            }).ToList(),
            Actions = page.Actions.Select(a => BuildPayloadAction(a, persistenceByAction)).ToList(),
            Tables = page.Tables.Select(t => new PayloadTable
            {
                TableId = t.Id,
                Columns = t.Columns.Select(c => c.Label ?? string.Empty).ToList(),
            }).ToList(),
        };
    }

    private static PayloadAction BuildPayloadAction(UiAction action, Dictionary<string, string> persistenceByAction)
    {
        persistenceByAction.TryGetValue(action.Id, out var persistenceKind);
        return new PayloadAction
        {
            ActionId = action.Id,
            Label = action.Label ?? string.Empty,
            Kind = action.Kind.ToString().ToLowerInvariant(),
            NavigationTarget = action.NavigationTarget,
            PersistenceKind = persistenceKind,
        };
    }

    private static string ModuleKey(string? controllerName)
    {
        if (string.IsNullOrWhiteSpace(controllerName)) return "general";
        var key = controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? controllerName[..^"Controller".Length]
            : controllerName;
        return key.ToLowerInvariant();
    }

    private static string ModuleKeyFromRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "general";
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0].ToLowerInvariant() : "general";
    }

    // ---- reduce ----

    private static TechnicalModuleResponse MergeTechnical(List<TechnicalModuleResponse> modules)
    {
        if (modules.Count == 0)
        {
            return new TechnicalModuleResponse { Title = "Technical Specification" };
        }

        var merged = new TechnicalModuleResponse
        {
            ModuleId = "all",
            Title = "Technical Specification",
        };

        foreach (var module in modules)
        {
            merged.Overview.AddRange(module.Overview);
            merged.Technologies.AddRange(module.Technologies);
            merged.Architecture.AddRange(module.Architecture);
            merged.Authentication.AddRange(module.Authentication);
            merged.Prerequisites.AddRange(module.Prerequisites);
            merged.Limitations.AddRange(module.Limitations);
            merged.Endpoints.AddRange(module.Endpoints);
        }

        merged.Endpoints = merged.Endpoints
            .GroupBy(e => e.EndpointId)
            .Select(g => g.First())
            .OrderBy(e => e.EndpointId, StringComparer.Ordinal)
            .ToList();

        return merged;
    }

    private static UserGuideModuleResponse MergeUserGuide(List<UserGuideModuleResponse> modules)
    {
        if (modules.Count == 0)
        {
            return new UserGuideModuleResponse { Title = "راهنمای کاربر" };
        }

        var merged = new UserGuideModuleResponse { ModuleId = "all", Title = "راهنمای کاربر" };

        foreach (var module in modules)
        {
            merged.Overview.AddRange(module.Overview);
            merged.Limitations.AddRange(module.Limitations);
            merged.Pages.AddRange(module.Pages);
        }

        merged.Pages = merged.Pages
            .GroupBy(p => p.PageId)
            .Select(g => g.First())
            .OrderBy(p => p.DisplayTitle, StringComparer.Ordinal)
            .ToList();

        return merged;
    }

    // ---- provider call + retry ----

    private async Task<LlmProviderResponse> CallWithRetryAsync(
        LlmProviderRequest request, SynthesisDocumentEntry entry, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await _provider.GenerateAsync(request, cancellationToken);
            entry.RequestCount++;

            if (response.IsSuccess)
            {
                return response;
            }

            if (!IsRetryable(response.ErrorCategory) || attempt >= _options.MaxRetries)
            {
                return response;
            }

            entry.RetryCount++;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private static bool IsRetryable(LlmErrorCategory? category) =>
        category is LlmErrorCategory.RateLimited or LlmErrorCategory.TransientProviderFailure or LlmErrorCategory.Timeout;

    // ---- response parsing ----

    private static bool TryParseTechnical(string content, out TechnicalModuleResponse? response)
    {
        response = null;
        try
        {
            response = JsonSerializer.Deserialize<TechnicalModuleResponse>(content, JsonOptions);
            return response is not null && response.DocumentType == "technical";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseUserGuide(string content, out UserGuideModuleResponse? response)
    {
        response = null;
        try
        {
            response = JsonSerializer.Deserialize<UserGuideModuleResponse>(content, JsonOptions);
            return response is not null && response.DocumentType == "userGuide";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ---- helpers ----

    private static T? Load<T>(string jobRootPath, string fileName)
    {
        var path = Path.Combine(jobRootPath, fileName);
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private static SynthesisStatus Aggregate(IEnumerable<SynthesisDocumentEntry> documents)
    {
        var statuses = documents.Select(d => d.Status).ToList();
        if (statuses.Any(s => s == SynthesisStatus.Failed)) return SynthesisStatus.Failed;
        if (statuses.Any(s => s == SynthesisStatus.Partial)) return SynthesisStatus.Partial;
        if (statuses.All(s => s == SynthesisStatus.NotApplicable)) return SynthesisStatus.NotApplicable;
        return SynthesisStatus.Complete;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static async Task WriteAtomicAsync(
        string jobRootPath, string fileName, string content, CancellationToken cancellationToken)
    {
        var finalPath = Path.Combine(jobRootPath, fileName);
        var tempPath = finalPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}
