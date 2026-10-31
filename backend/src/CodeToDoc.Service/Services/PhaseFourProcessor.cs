using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class PhaseFourProcessor : IPhaseFourProcessor
{
    private const string SkeletonFileName = "correlation_skeleton.json";
    private const string TechnicalSkeletonFileName = "technical_skeleton.json";
    private const string UiSkeletonFileName = "ui_skeleton.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ICorrelationEngine _engine;
    private readonly ILogger<PhaseFourProcessor> _logger;

    public PhaseFourProcessor(ICorrelationEngine engine, ILogger<PhaseFourProcessor> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task<CorrelationSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var technicalPath = Path.Combine(workspace.JobRootPath, TechnicalSkeletonFileName);
        var uiPath = Path.Combine(workspace.JobRootPath, UiSkeletonFileName);

        if (!File.Exists(technicalPath) || !File.Exists(uiPath))
        {
            var blocked = new CorrelationSkeleton
            {
                JobId = workspace.JobId,
                GeneratedAtUtc = DateTime.UtcNow,
                Status = CorrelationStatus.Blocked,
            };

            blocked.Diagnostics.Add(new UiDiagnostic
            {
                Code = "corr.blocked",
                Severity = DiagnosticSeverity.Error,
                Message = "Phase 4 requires technical_skeleton.json and ui_skeleton.json in the job workspace.",
            });

            await WriteAtomicAsync(workspace.JobRootPath, blocked, cancellationToken);
            return blocked;
        }

        var technical = JsonSerializer.Deserialize<TechnicalSkeleton>(
            await File.ReadAllTextAsync(technicalPath, cancellationToken), JsonOptions)!;
        var ui = JsonSerializer.Deserialize<UiSkeleton>(
            await File.ReadAllTextAsync(uiPath, cancellationToken), JsonOptions)!;

        var sourceFiles = ReadSourceFiles(workspace.SourcePath, cancellationToken);
        var correlation = _engine.Correlate(technical, ui, sourceFiles, workspace.JobId);

        await WriteAtomicAsync(workspace.JobRootPath, correlation, cancellationToken);

        _logger.LogInformation("Phase 4 generated {SkeletonFileName} for job {JobId} (status {Status}).",
            SkeletonFileName, workspace.JobId, correlation.Status);

        return correlation;
    }

    private static List<SourceFile> ReadSourceFiles(string sourcePath, CancellationToken cancellationToken)
    {
        var files = new List<SourceFile>();

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
            var fileName = Path.GetFileName(relative);

            if (fileName is TechnicalSkeletonFileName or UiSkeletonFileName or SkeletonFileName)
            {
                continue;
            }

            files.Add(new SourceFile { RelativePath = relative, Content = File.ReadAllText(file) });
        }

        return files;
    }

    private static async Task WriteAtomicAsync(
        string jobRootPath, CorrelationSkeleton skeleton, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        var json = JsonSerializer.Serialize(skeleton, options);
        var finalPath = Path.Combine(jobRootPath, SkeletonFileName);
        var tempPath = finalPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}
