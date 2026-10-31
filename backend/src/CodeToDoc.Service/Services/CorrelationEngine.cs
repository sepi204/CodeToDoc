using System.Text;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToDoc.Service.Services;

public sealed class CorrelationEngine : ICorrelationEngine
{
    public CorrelationSkeleton Correlate(
        TechnicalSkeleton technical,
        UiSkeleton ui,
        IReadOnlyList<SourceFile> sourceFiles,
        string jobId)
    {
        var skeleton = new CorrelationSkeleton
        {
            JobId = jobId,
            GeneratedAtUtc = DateTime.UtcNow,
            Status = CorrelationStatus.Complete,
        };

        if (technical.Endpoints.Count == 0)
        {
            skeleton.Status = CorrelationStatus.NotApplicable;
            skeleton.Diagnostics.Add(new UiDiagnostic
            {
                Code = "corr.nobackend",
                Severity = DiagnosticSeverity.Info,
                Message = "No backend endpoints are available for correlation.",
            });
            return skeleton;
        }

        var uiActions = CollectActions(ui);
        if (uiActions.Count == 0)
        {
            skeleton.Status = CorrelationStatus.NotApplicable;
            skeleton.Diagnostics.Add(new UiDiagnostic
            {
                Code = "corr.noui",
                Severity = DiagnosticSeverity.Info,
                Message = "No UI actions with API references are available for correlation.",
            });
            return skeleton;
        }

        var endpointIndex = BuildEndpointIndex(technical.Endpoints);
        var counter = 0;

        foreach (var (page, action) in uiActions)
        {
            foreach (var apiRef in action.ApiReferences)
            {
                var matches = FindCandidates(apiRef, endpointIndex);

                if (matches.Count == 0)
                {
                    skeleton.Diagnostics.Add(new UiDiagnostic
                    {
                        Code = "corr.unmatched",
                        Severity = DiagnosticSeverity.Info,
                        Message = $"No backend endpoint matches UI action '{action.Label}' ({apiRef.Method ?? "?"} {apiRef.Url ?? apiRef.UrlExpression}).",
                    });
                    continue;
                }

                if (matches.Count > 1)
                {
                    skeleton.Diagnostics.Add(new UiDiagnostic
                    {
                        Code = "corr.ambiguous",
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"UI action '{action.Label}' matches {matches.Count} backend endpoints.",
                    });
                }

                foreach (var match in matches)
                {
                    skeleton.Correlations.Add(BuildCorrelation(
                        page, action, apiRef, match, sourceFiles, counter++));
                }
            }
        }

        skeleton.Correlations = skeleton.Correlations
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        skeleton.Status = skeleton.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            ? CorrelationStatus.Partial
            : CorrelationStatus.Complete;

        return skeleton;
    }

    // ---- collection ----

    private static List<(UiPage Page, UiAction Action)> CollectActions(UiSkeleton ui)
    {
        var actions = new List<(UiPage, UiAction)>();
        foreach (var page in ui.Pages)
        {
            foreach (var action in page.Actions)
            {
                if (action.ApiReferences.Count > 0)
                {
                    actions.Add((page, action));
                }
            }
        }

        return actions;
    }

    private static List<(EndpointModel Endpoint, string EndpointId)> BuildEndpointIndex(
        IReadOnlyList<EndpointModel> endpoints)
    {
        return endpoints.Select((endpoint, index) => (endpoint, $"ep_{index}")).ToList();
    }

    // ---- matching ----

    private sealed class EndpointMatch
    {
        public EndpointModel Endpoint { get; init; } = new();
        public string EndpointId { get; init; } = string.Empty;
        public bool MethodMatch { get; init; }
        public bool PathMatch { get; init; }
        public bool TokenMatch { get; init; }
        public string MatchReason { get; init; } = string.Empty;
    }

    private static List<EndpointMatch> FindCandidates(
        UiApiReference apiRef,
        List<(EndpointModel Endpoint, string EndpointId)> index)
    {
        var uiMethod = NormalizeMethod(apiRef.Method);
        var uiUrl = apiRef.Url is not null ? NormalizePath(apiRef.Url) : null;
        var staticPrefix = apiRef.Url is null && apiRef.UrlExpression is not null
            ? ExtractStaticPrefix(apiRef.UrlExpression)
            : null;

        if (uiUrl is null && staticPrefix is null)
        {
            return new List<EndpointMatch>();
        }

        var candidates = new List<EndpointMatch>();

        foreach (var (endpoint, endpointId) in index)
        {
            var endpointPath = NormalizeEndpointPath(endpoint);
            var endpointMethod = NormalizeMethod(endpoint.Method);

            var methodMatch = uiMethod.Length > 0 && endpointMethod.Length > 0 && uiMethod == endpointMethod;

            bool pathMatch;
            string matchReason;

            if (uiUrl is not null)
            {
                pathMatch = PathMatches(uiUrl, endpointPath);
                matchReason = pathMatch
                    ? (uiUrl.Equals(endpointPath, StringComparison.OrdinalIgnoreCase) ? "method+path exact match" : "method+path template match")
                    : string.Empty;
            }
            else
            {
                pathMatch = staticPrefix is not null &&
                    endpointPath.StartsWith(staticPrefix, StringComparison.OrdinalIgnoreCase);
                matchReason = pathMatch ? "static-prefix partial match" : string.Empty;
            }

            if (!pathMatch)
            {
                continue;
            }

            // If both methods are known and they differ, this is not a candidate.
            if (uiMethod.Length > 0 && endpointMethod.Length > 0 && uiMethod != endpointMethod)
            {
                continue;
            }

            var tokenMatch = SharesToken(uiUrl ?? staticPrefix ?? string.Empty, endpoint);

            candidates.Add(new EndpointMatch
            {
                Endpoint = endpoint,
                EndpointId = endpointId,
                MethodMatch = methodMatch,
                PathMatch = pathMatch,
                TokenMatch = tokenMatch,
                MatchReason = matchReason,
            });
        }

        return candidates;
    }

    private static bool PathMatches(string uiPath, string endpointPath)
    {
        if (string.Equals(uiPath, endpointPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var uiSegments = uiPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var endpointSegments = endpointPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (uiSegments.Length != endpointSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < uiSegments.Length; i++)
        {
            if (endpointSegments[i].StartsWith('{') && endpointSegments[i].EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(uiSegments[i], endpointSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SharesToken(string value, EndpointModel endpoint)
    {
        var tokens = value.Split(new[] { '/', '-', '_', '.', '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var controller = (endpoint.ControllerName ?? string.Empty).ToLowerInvariant();
        if (controller.EndsWith("controller", StringComparison.OrdinalIgnoreCase))
        {
            controller = controller[..^"controller".Length];
        }

        return tokens.Contains(controller);
    }

    private static double Score(bool methodMatch, bool pathMatch, bool tokenMatch, bool dtoMatch, bool authMatch)
    {
        var score = 0.0;

        if (methodMatch && pathMatch)
        {
            score += 0.7;
        }
        else if (pathMatch)
        {
            score += 0.4; // path matches but method is unknown
        }

        if (tokenMatch) score += 0.1;
        if (dtoMatch) score += 0.1;
        if (authMatch) score += 0.1;

        return Math.Round(Math.Min(1.0, score), 2);
    }

    // ---- normalization ----

    private static string NormalizeMethod(string? method) => method?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }
        }

        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        return path;
    }

    private static string NormalizeEndpointPath(EndpointModel endpoint)
    {
        var path = NormalizePath(endpoint.Path ?? string.Empty);

        if (path.Contains("[controller]", StringComparison.OrdinalIgnoreCase))
        {
            var controller = endpoint.ControllerName ?? string.Empty;
            if (controller.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                controller = controller[..^"Controller".Length];
            }

            path = Regex.Replace(path, @"\[controller\]", controller.ToLowerInvariant(), RegexOptions.IgnoreCase);
        }

        return path;
    }

    private static string ExtractStaticPrefix(string expression)
    {
        var trimmed = expression.Trim();
        var index = trimmed.IndexOfAny(new[] { '$', '{', '+' });
        var prefix = index >= 0 ? trimmed[..index] : trimmed;
        return NormalizePath(prefix);
    }

    // ---- correlation building ----

    private Correlation BuildCorrelation(
        UiPage page,
        UiAction action,
        UiApiReference apiRef,
        EndpointMatch match,
        IReadOnlyList<SourceFile> sourceFiles,
        int index)
    {
        var endpoint = match.Endpoint;
        var fieldMapping = BuildFieldMapping(page, action, endpoint.RequestDto);

        var dtoMatch = fieldMapping.Mappings.Count > 0;
        var authMatch = endpoint.AuthRequired;
        var confidence = Score(match.MethodMatch, match.PathMatch, match.TokenMatch, dtoMatch, authMatch);

        var reasons = new List<string>();
        if (match.MatchReason.Length > 0)
        {
            reasons.Add(match.MatchReason);
        }

        if (match.TokenMatch)
        {
            reasons.Add("page/controller token overlap");
        }

        if (dtoMatch)
        {
            reasons.Add("form fields match request dto");
        }

        if (authMatch)
        {
            reasons.Add("auth requirements align");
        }

        var scenarioId = page.Scenarios.FirstOrDefault(s => s.TriggerElementId == action.Id)?.Id;

        var correlation = new Correlation
        {
            Id = $"corr_{index}",
            RepositoryId = page.RepositoryId,
            PageId = page.Id,
            ActionId = action.Id,
            ScenarioId = scenarioId,
            Ui = new CorrelationUi
            {
                PageRoute = page.Route,
                ActionLabel = action.Label,
                ActionKind = action.Kind,
                ApiReference = new CorrelationApiReference
                {
                    Method = apiRef.Method,
                    Url = apiRef.Url,
                    UrlExpression = apiRef.UrlExpression,
                    EvidenceIds = apiRef.EvidenceIds,
                },
            },
            Backend = new CorrelationBackend
            {
                EndpointId = match.EndpointId,
                Method = endpoint.Method,
                PathTemplate = NormalizeEndpointPath(endpoint),
                ControllerName = endpoint.ControllerName,
                ActionName = endpoint.ActionName,
                AuthRequired = endpoint.AuthRequired,
                Roles = endpoint.Roles,
                RequestDtoType = endpoint.RequestDto?.TypeName,
                ResponseDtoType = endpoint.ResponseDto?.TypeName,
            },
            FieldMapping = fieldMapping,
            Persistence = AnalyzePersistence(endpoint, sourceFiles),
            Confidence = confidence,
            MatchReasons = reasons,
        };

        return correlation;
    }

    private static CorrelationFieldMapping BuildFieldMapping(UiPage page, UiAction action, DtoModel? requestDto)
    {
        var mapping = new CorrelationFieldMapping();

        if (requestDto is null || requestDto.Properties.Count == 0)
        {
            return mapping;
        }

        var form = page.Forms.FirstOrDefault(f => f.Id == action.FormId);
        var uiFields = form?.Fields ?? new List<UiField>();
        var mappedDtoProps = new Dictionary<string, UiField>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in uiFields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }

            var match = FindDtoProperty(field.Name, requestDto.Properties);
            if (match.Property is null)
            {
                continue;
            }

            mapping.Mappings.Add(new FieldMapping
            {
                UiFieldName = field.Name,
                DtoProperty = match.Property.Name,
                MappingKind = match.Kind,
                Confidence = match.Confidence,
            });

            mappedDtoProps[match.Property.Name] = field;
        }

        foreach (var property in requestDto.Properties.Where(p => p.IsRequired))
        {
            if (!mappedDtoProps.TryGetValue(property.Name, out var uiField))
            {
                mapping.MissingRequiredOnUi.Add(new MissingRequiredField
                {
                    DtoProperty = property.Name,
                    IsRequiredOnBackend = true,
                });
            }
            else if (uiField.Required != true)
            {
                mapping.RequiredOnBackendButNotMarkedRequiredOnUi.Add(property.Name);
            }
        }

        return mapping;
    }

    private static (PropertyModel? Property, string Kind, double Confidence) FindDtoProperty(
        string uiName, IReadOnlyList<PropertyModel> properties)
    {
        foreach (var property in properties)
        {
            if (string.Equals(uiName, property.Name, StringComparison.OrdinalIgnoreCase))
            {
                return (property, "nameMatch", 0.9);
            }
        }

        var normalized = NormalizeName(uiName);
        foreach (var property in properties)
        {
            if (string.Equals(normalized, NormalizeName(property.Name), StringComparison.OrdinalIgnoreCase))
            {
                return (property, "caseTransform", 0.8);
            }
        }

        return (null, string.Empty, 0.0);
    }

    private static string NormalizeName(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    // ---- persistence ----

    private CorrelationPersistence AnalyzePersistence(EndpointModel endpoint, IReadOnlyList<SourceFile> sourceFiles)
    {
        var controllerFile = sourceFiles.FirstOrDefault(f =>
            f.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            f.Content.Contains($"class {endpoint.ControllerName}"));

        if (controllerFile is null)
        {
            return HeuristicPersistence(endpoint);
        }

        var tree = CSharpSyntaxTree.ParseText(controllerFile.Content);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == endpoint.ActionName);

        if (method is null)
        {
            return HeuristicPersistence(endpoint);
        }

        var body = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? string.Empty;

        var addMatch = Regex.Match(body, @"\.(\w+)\.Add(?:Async)?\(");
        var addNewMatch = Regex.Match(body, @"\.Add(?:Async)?\(\s*new\s+(\w+)");
        var updateMatch = Regex.Match(body, @"\.(\w+)\.Update\(");
        var removeMatch = Regex.Match(body, @"\.(\w+)\.Remove\(");
        var saveChanges = Regex.IsMatch(body, @"SaveChanges(?:Async)?\(");

        if (saveChanges && addMatch.Success)
        {
            return new CorrelationPersistence
            {
                Kind = PersistenceKind.Evidence,
                Operation = "create",
                EntityType = Singularize(addMatch.Groups[1].Value),
            };
        }

        if (saveChanges && addNewMatch.Success)
        {
            return new CorrelationPersistence
            {
                Kind = PersistenceKind.Evidence,
                Operation = "create",
                EntityType = addNewMatch.Groups[1].Value,
            };
        }

        if (saveChanges && updateMatch.Success)
        {
            return new CorrelationPersistence
            {
                Kind = PersistenceKind.Evidence,
                Operation = "update",
                EntityType = Singularize(updateMatch.Groups[1].Value),
            };
        }

        if (saveChanges && removeMatch.Success)
        {
            return new CorrelationPersistence
            {
                Kind = PersistenceKind.Evidence,
                Operation = "delete",
                EntityType = Singularize(removeMatch.Groups[1].Value),
            };
        }

        return HeuristicPersistence(endpoint);
    }

    private static CorrelationPersistence HeuristicPersistence(EndpointModel endpoint)
    {
        var operation = endpoint.Method?.ToUpperInvariant() switch
        {
            "POST" => "create",
            "PUT" or "PATCH" => "update",
            "DELETE" => "delete",
            _ => null,
        };

        return new CorrelationPersistence
        {
            Kind = PersistenceKind.Heuristic,
            Operation = operation,
        };
    }

    private static string Singularize(string name)
    {
        if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3)
        {
            return name[..^3] + "y";
        }

        if (name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal))
        {
            return name[..^1];
        }

        return name;
    }
}
