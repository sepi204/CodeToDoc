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
    private readonly ILogger<PhaseTwoProcessor> _logger;

    public PhaseTwoProcessor(
        IManifestParser manifestParser,
        IArchitectureInferenceEngine inferenceEngine,
        ICSharpAstParser csharpAstParser,
        ILogger<PhaseTwoProcessor> logger)
    {
        _manifestParser = manifestParser;
        _inferenceEngine = inferenceEngine;
        _csharpAstParser = csharpAstParser;
        _logger = logger;
    }

    public async Task<TechnicalSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var sourceFiles = ReadSourceFiles(workspace.SourcePath, cancellationToken);
        var manifest = await _manifestParser.ParseAsync(workspace.SourcePath, cancellationToken);
        manifest.PrimaryLanguage = DetectPrimaryLanguage(sourceFiles);

        var inference = _inferenceEngine.Infer(manifest);

        var endpoints = new List<EndpointModel>();
        endpoints.AddRange(_csharpAstParser.Parse(sourceFiles));
        endpoints.AddRange(ScriptRouteParser.Parse(sourceFiles));

        var skeleton = new TechnicalSkeleton
        {
            JobId = workspace.JobId,
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = inference.Summary,
            Dependencies = inference.Dependencies,
            Endpoints = endpoints,
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
