using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class PhaseThreeProcessor : IPhaseThreeProcessor
{
    private const string SkeletonFileName = "ui_skeleton.json";

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".next", ".nuxt", ".turbo",
        "dist", "build", "out", "coverage", "__pycache__", ".pytest_cache",
        ".mypy_cache", ".idea", ".vs", "venv", "env",
    };

    private static readonly HashSet<string> IgnoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "technical_skeleton.json", "ui_skeleton.json",
    };

    private readonly IFrontendProjectDetector _detector;
    private readonly IUiRouteDiscoverer _routeDiscoverer;
    private readonly IUiSourceParser _parser;
    private readonly IUiComponentResolver _resolver;
    private readonly IUiScenarioExtractor _scenarioExtractor;
    private readonly PhaseThreeProcessingOptions _options;
    private readonly ILogger<PhaseThreeProcessor> _logger;

    public PhaseThreeProcessor(
        IFrontendProjectDetector detector,
        IUiRouteDiscoverer routeDiscoverer,
        IUiSourceParser parser,
        IUiComponentResolver resolver,
        IUiScenarioExtractor scenarioExtractor,
        IOptions<PhaseThreeProcessingOptions> options,
        ILogger<PhaseThreeProcessor> logger)
    {
        _detector = detector;
        _routeDiscoverer = routeDiscoverer;
        _parser = parser;
        _resolver = resolver;
        _scenarioExtractor = scenarioExtractor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UiSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var skeleton = new UiSkeleton
        {
            JobId = workspace.JobId,
            GeneratedAtUtc = DateTime.UtcNow,
            Status = UiStatus.NotApplicable,
        };

        var sourceFiles = ReadSourceFiles(workspace.SourcePath, skeleton.Diagnostics, cancellationToken);
        var projects = _detector.Detect(workspace.SourcePath, sourceFiles);

        if (projects.Count == 0)
        {
            skeleton.Diagnostics.Add(new UiDiagnostic
            {
                Code = "ui.none",
                Severity = DiagnosticSeverity.Info,
                Message = "No frontend project detected in the sanitized workspace.",
            });

            await WriteAtomicAsync(workspace.JobRootPath, skeleton, cancellationToken);
            return skeleton;
        }

        var allEvidence = new List<UiEvidence>();
        var sharedComponents = new List<SharedComponent>();
        var diagnostics = new List<UiDiagnostic>(skeleton.Diagnostics);

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            skeleton.Repositories.Add(new UiRepository
            {
                Id = project.Id,
                Name = project.Name,
                Root = project.Root,
                Framework = project.Framework,
            });

            var routes = _routeDiscoverer.Discover(project);

            foreach (var route in routes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!route.IsResolved)
                {
                    diagnostics.Add(new UiDiagnostic
                    {
                        Code = "ui.route.unresolved",
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Route could not be resolved for '{route.SourceFile}'. Unsupported routing construct.",
                        SourceReference = new UiSourceReference { FilePath = route.SourceFile },
                    });
                    continue;
                }

                var page = BuildPage(project, route, sourceFiles, allEvidence, sharedComponents, diagnostics, cancellationToken);
                if (page is not null)
                {
                    skeleton.Pages.Add(page);
                }
            }
        }

        skeleton.Evidence = allEvidence;
        skeleton.SharedComponents = sharedComponents;
        skeleton.Diagnostics = diagnostics;
        skeleton.Status = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            ? UiStatus.Partial
            : UiStatus.Complete;

        await WriteAtomicAsync(workspace.JobRootPath, skeleton, cancellationToken);

        _logger.LogInformation("Phase 3 generated {SkeletonFileName} for job {JobId} (status {Status}).",
            SkeletonFileName, workspace.JobId, skeleton.Status);

        return skeleton;
    }

    private UiPage? BuildPage(
        FrontendProject project,
        DiscoveredRoute route,
        IReadOnlyList<SourceFile> sourceFiles,
        List<UiEvidence> allEvidence,
        List<SharedComponent> sharedComponents,
        List<UiDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageFile = sourceFiles.FirstOrDefault(f =>
            f.RelativePath.Replace('\\', '/').Equals(route.SourceFile, StringComparison.OrdinalIgnoreCase));

        if (pageFile is null)
        {
            return null;
        }

        if (LooksMalformed(pageFile.Content))
        {
            diagnostics.Add(new UiDiagnostic
            {
                Code = "ui.parse.malformed",
                Severity = DiagnosticSeverity.Error,
                Message = $"Source file '{route.SourceFile}' appears malformed; extracted elements may be partial.",
                SourceReference = new UiSourceReference { FilePath = route.SourceFile },
            });
        }

        var pageSource = _parser.Parse(pageFile, project);
        var components = _resolver.ResolveImports(pageFile, pageSource, project, sourceFiles, d => diagnostics.Add(d));

        // Merge page + resolved components into an effective source.
        var effective = new ParsedUiSource { Title = pageSource.Title };
        effective.Forms.AddRange(pageSource.Forms);
        effective.StandaloneFields.AddRange(pageSource.StandaloneFields);
        effective.Actions.AddRange(pageSource.Actions);
        effective.Tables.AddRange(pageSource.Tables);
        effective.Navigation.AddRange(pageSource.Navigation);
        effective.Fetches.AddRange(pageSource.Fetches);
        effective.RouterPushes.AddRange(pageSource.RouterPushes);
        allEvidence.AddRange(pageSource.Evidence);

        foreach (var component in components)
        {
            effective.Forms.AddRange(component.Source.Forms);
            effective.StandaloneFields.AddRange(component.Source.StandaloneFields);
            effective.Actions.AddRange(component.Source.Actions);
            effective.Tables.AddRange(component.Source.Tables);
            effective.Navigation.AddRange(component.Source.Navigation);
            effective.Fetches.AddRange(component.Source.Fetches);
            effective.RouterPushes.AddRange(component.Source.RouterPushes);
            allEvidence.AddRange(component.Source.Evidence);

            if (effective.Title is null)
            {
                effective.Title = component.Source.Title;
            }

            sharedComponents.Add(new SharedComponent
            {
                Id = $"component_{component.Name}",
                Name = component.Name,
                SourceFile = component.Source.Evidence.FirstOrDefault()?.FilePath ?? string.Empty,
                Forms = component.Source.Forms,
                Fields = component.Source.StandaloneFields,
                EvidenceIds = component.Source.Evidence.Select(e => e.Id).ToList(),
            });
        }

        // Associate POST fetches with submit actions.
        var submitActions = effective.Actions.Where(a => a.Kind == ActionKind.SubmitForm).ToList();
        var postFetches = effective.Fetches.Where(f => string.Equals(f.Method, "POST", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var action in submitActions)
        {
            foreach (var fetch in postFetches)
            {
                action.ApiReferences.Add(new UiApiReference
                {
                    Method = fetch.Method,
                    Url = fetch.Url,
                    UrlExpression = fetch.UrlExpression,
                });
            }
        }

        var scenarios = _scenarioExtractor.Extract(effective, effective.Actions);

        return new UiPage
        {
            Id = $"page_{project.Id}_{Sanitize(route.Route)}",
            RepositoryId = project.Id,
            Route = route.Route,
            SourceFile = route.SourceFile,
            RouteParameters = route.RouteParameters,
            Title = effective.Title,
            Forms = effective.Forms,
            Actions = effective.Actions,
            Tables = effective.Tables,
            Scenarios = scenarios,
            EvidenceIds = allEvidence.Where(e => e.FilePath.Equals(route.SourceFile, StringComparison.OrdinalIgnoreCase)).Select(e => e.Id).ToList(),
        };
    }

    private List<SourceFile> ReadSourceFiles(
        string sourcePath,
        List<UiDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var files = new List<SourceFile>();

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
            var fileName = Path.GetFileName(relative);

            if (IgnoredFiles.Contains(fileName))
            {
                continue;
            }

            var segments = relative.Split('/');
            if (segments.Any(s => IgnoredDirectories.Contains(s)))
            {
                continue;
            }

            var info = new FileInfo(file);
            if (info.Length > _options.MaxSourceFileBytes)
            {
                diagnostics.Add(new UiDiagnostic
                {
                    Code = "ui.limit.fileSize",
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"File '{relative}' exceeds the maximum source file size and was skipped.",
                    SourceReference = new UiSourceReference { FilePath = relative },
                });
                continue;
            }

            if (files.Count >= _options.MaxAnalyzedFiles)
            {
                diagnostics.Add(new UiDiagnostic
                {
                    Code = "ui.limit.fileCount",
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Maximum analyzed file count ({_options.MaxAnalyzedFiles}) reached; remaining files skipped.",
                });
                break;
            }

            files.Add(new SourceFile { RelativePath = relative, Content = File.ReadAllText(file) });
        }

        return files;
    }

    private static bool LooksMalformed(string content)
    {
        var formOpen = Regex.Matches(content, @"<form\b", RegexOptions.IgnoreCase).Count;
        var formClose = Regex.Matches(content, @"</form\s*>", RegexOptions.IgnoreCase).Count;
        if (formOpen != formClose)
        {
            return true;
        }

        var divOpen = Regex.Matches(content, @"<div\b", RegexOptions.IgnoreCase).Count;
        var divClose = Regex.Matches(content, @"</div\s*>", RegexOptions.IgnoreCase).Count;
        return divOpen != divClose;
    }

    private static async Task WriteAtomicAsync(string jobRootPath, UiSkeleton skeleton, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        var json = JsonSerializer.Serialize(skeleton, options);
        var finalPath = Path.Combine(jobRootPath, SkeletonFileName);
        var tempPath = finalPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "root";
        }

        var builder = new System.Text.StringBuilder();
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        var result = builder.ToString().Trim('_');
        return result.Length == 0 ? "root" : result;
    }
}
