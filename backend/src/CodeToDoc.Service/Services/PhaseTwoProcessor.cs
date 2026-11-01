using System.Text.Json;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class PhaseTwoProcessor : IPhaseTwoProcessor
{
    private const string SkeletonFileName = "technical_skeleton.json";

    private readonly IManifestParser _manifestParser;
    private readonly IArchitectureInferenceEngine _inferenceEngine;
    private readonly ICSharpAstParser _csharpAstParser;
    private readonly IProjectRootDiscoverer _rootDiscoverer;
    private readonly ILogger<PhaseTwoProcessor> _logger;

    public PhaseTwoProcessor(
        IManifestParser manifestParser,
        IArchitectureInferenceEngine inferenceEngine,
        ICSharpAstParser csharpAstParser,
        IProjectRootDiscoverer rootDiscoverer,
        ILogger<PhaseTwoProcessor> logger)
    {
        _manifestParser = manifestParser;
        _inferenceEngine = inferenceEngine;
        _csharpAstParser = csharpAstParser;
        _rootDiscoverer = rootDiscoverer;
        _logger = logger;
    }

    public async Task<TechnicalSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var sourceFiles = ReadSourceFiles(workspace.SourcePath, cancellationToken);
        var manifest = await _manifestParser.ParseAsync(workspace.SourcePath, cancellationToken);
        manifest.PrimaryLanguage = DetectPrimaryLanguage(sourceFiles);

        var inference = _inferenceEngine.Infer(manifest);

        // Multi-project / monorepo detection (e.g. root Astro + api/ NestJS).
        var roots = _rootDiscoverer.Discover(sourceFiles);

        var endpoints = new List<EndpointModel>();
        endpoints.AddRange(_csharpAstParser.Parse(sourceFiles));
        endpoints.AddRange(ScriptRouteParser.Parse(sourceFiles));
        endpoints.AddRange(NestJsControllerParser.Parse(sourceFiles, out var entities));
        endpoints.AddRange(AstroRouteParser.Parse(sourceFiles));
        endpoints = Deduplicate(endpoints);

        var projects = roots.Select(r => new DetectedProjectInfo
        {
            Root = r.Root,
            Framework = r.Framework,
            Language = r.Language,
        }).ToList();

        // Fallback framework naming: prefer most specific backend framework.
        var summary = PromoteFrameworks(inference.Summary, roots, endpoints);

        var skeleton = new TechnicalSkeleton
        {
            JobId = workspace.JobId,
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = summary,
            Dependencies = inference.Dependencies,
            Endpoints = endpoints,
            Projects = projects,
            Entities = entities,
            Warnings = manifest.Warnings,
        };

        await WriteSkeletonAsync(workspace.JobRootPath, skeleton, cancellationToken);

        _logger.LogInformation("Phase 2 generated {SkeletonFileName} for job {JobId}.",
            SkeletonFileName, workspace.JobId);

        return skeleton;
    }

    private static List<SourceFile> ReadSourceFiles(string sourcePath, CancellationToken cancellationToken)
    {
        var files = new List<SourceFile>();

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
            var content = File.ReadAllText(file);

            files.Add(new SourceFile { RelativePath = relative, Content = content });
        }

        return files;
    }

    private static string? DetectPrimaryLanguage(IReadOnlyList<SourceFile> sourceFiles)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
            var language = extension switch
            {
                ".cs" => "C#",
                ".ts" or ".tsx" => "TypeScript",
                ".js" or ".jsx" => "JavaScript",
                ".py" => "Python",
                ".java" => "Java",
                ".go" => "Go",
                ".rs" => "Rust",
                ".php" => "PHP",
                _ => null,
            };

            if (language is null)
            {
                continue;
            }

            counts[language] = counts.GetValueOrDefault(language) + 1;
        }

        return counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
    }

    private static List<EndpointModel> Deduplicate(List<EndpointModel> endpoints)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<EndpointModel>();
        foreach (var e in endpoints)
        {
            var key = $"{e.Method} {e.Path} {e.ControllerName}";
            if (seen.Add(key))
            {
                result.Add(e);
            }
        }
        return result.OrderBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Method, StringComparer.Ordinal).ToList();
    }

    private static Dtos.ArchitectureSummary PromoteFrameworks(
        Dtos.ArchitectureSummary summary,
        List<Models.DetectedProjectRoot> roots,
        List<EndpointModel> endpoints)
    {
        var frameworks = roots.Select(r => r.Framework).Where(f => f != "Node.js").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (frameworks.Count > 0)
        {
            var hasNest = frameworks.Any(f => f.Equals("NestJS", StringComparison.OrdinalIgnoreCase));
            var hasAstro = frameworks.Any(f => f.Equals("Astro", StringComparison.OrdinalIgnoreCase));
            if (hasNest && hasAstro)
            {
                summary.Framework = "Astro (SSR frontend) + NestJS (API)";
            }
            else if (summary.Framework is null || summary.Framework is "React" or "Node.js")
            {
                summary.Framework = string.Join(" + ", frameworks);
            }
            else if (!frameworks.Any(f => summary.Framework.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                summary.Framework = summary.Framework + " + " + string.Join(" + ", frameworks);
            }
        }

        if (endpoints.Any(e => e.AuthRequired) && string.IsNullOrWhiteSpace(summary.Auth))
        {
            summary.Auth = "Guards / JWT (detected from controllers)";
        }

        return summary;
    }

    private static async Task WriteSkeletonAsync(
        string jobRootPath,
        TechnicalSkeleton skeleton,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize(skeleton, options);
        var path = Path.Combine(jobRootPath, SkeletonFileName);

        await File.WriteAllTextAsync(path, json, cancellationToken);
    }
}
