using System.Diagnostics;
using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Exceptions;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class PhaseOneProcessor : IPhaseOneProcessor
{
    private readonly PhaseOneProcessingOptions _options;
    private readonly IJobWorkspaceManager _workspaceManager;
    private readonly IArchiveExtractor _extractor;
    private readonly IFileTreeBuilder _fileTreeBuilder;
    private readonly IProjectRootDiscoverer _rootDiscoverer;
    private readonly ILogger<PhaseOneProcessor> _logger;

    public PhaseOneProcessor(
        IOptions<PhaseOneProcessingOptions> options,
        IJobWorkspaceManager workspaceManager,
        IArchiveExtractor extractor,
        IFileTreeBuilder fileTreeBuilder,
        IProjectRootDiscoverer rootDiscoverer,
        ILogger<PhaseOneProcessor> logger)
    {
        _options = options.Value;
        _workspaceManager = workspaceManager;
        _extractor = extractor;
        _fileTreeBuilder = fileTreeBuilder;
        _rootDiscoverer = rootDiscoverer;
        _logger = logger;
    }

    public async Task<PhaseOneResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        var outcome = await ProcessWithWorkspaceAsync(archives, cancellationToken);
        return outcome.Result;
    }

    public async Task<PhaseOneWorkspaceResult> ProcessWithWorkspaceAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        return await ProcessWithWorkspaceAsync(archives, null, cancellationToken);
    }

    public async Task<PhaseOneWorkspaceResult> ProcessWithWorkspaceAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();

        ValidateUploads(archives);

        var workspace = _workspaceManager.CreateWorkspace(jobId);
        _logger.LogInformation("Phase 1 job {JobId} started with {ArchiveCount} archive(s).",
            workspace.JobId, archives.Count);

        var context = new ExtractionContext();
        var archiveResults = new List<ArchiveResult>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalStackDetectionMs = 0;

        try
        {
            foreach (var archive in archives)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var safeName = GetUniqueSafeName(SanitizeSafeName(archive.FileName), usedNames);
                var extraction = await _extractor.ExtractAsync(
                    archive,
                    workspace.SourcePath,
                    safeName,
                    context,
                    cancellationToken);

                totalStackDetectionMs += extraction.StackDetectionMs;

                archiveResults.Add(new ArchiveResult
                {
                    OriginalFileName = archive.FileName,
                    SafeDirectoryName = safeName,
                    TotalEntries = extraction.TotalEntries,
                    ExtractedFileCount = extraction.ExtractedFileCount,
                    SkippedEntryCount = extraction.SkippedEntryCount,
                    ExtractedBytes = extraction.ExtractedBytes,
                    DetectedStacks = extraction.DetectedStacks,
                    Warnings = extraction.Warnings,
                });
            }

            var fileTree = _fileTreeBuilder.Build(workspace.SourcePath);
            var textFileTree = _fileTreeBuilder.BuildText(fileTree);
            var aggregateStacks = AggregateStacks(archiveResults.SelectMany(r => r.DetectedStacks));
            var projectRoots = DiscoverProjectRoots(workspace.SourcePath);

            overallStopwatch.Stop();

            _logger.LogInformation("Phase 1 job {JobId} completed successfully.", workspace.JobId);

            var result = new PhaseOneResult
            {
                JobId = workspace.JobId,
                Status = Enums.ProcessingStatus.Succeeded,
                ArchiveCount = archiveResults.Count,
                TotalArchiveEntries = context.TotalArchiveEntries,
                ExtractedFileCount = context.TotalExtractedFileCount,
                SkippedEntryCount = context.TotalSkippedEntries,
                TotalExtractedBytes = context.TotalExtractedBytes,
                AggregateDetectedStacks = aggregateStacks,
                ProjectRoots = projectRoots,
                ArchiveResults = archiveResults,
                FileTree = fileTree,
                TextFileTree = textFileTree,
                Warnings = archiveResults.SelectMany(r => r.Warnings).ToList(),
                ProcessingDurationMs = overallStopwatch.ElapsedMilliseconds,
                StackDetectionDurationMs = totalStackDetectionMs,
            };

            return new PhaseOneWorkspaceResult { Result = result, Workspace = workspace };
        }
        catch
        {
            if (_options.DeleteJobDirectoryOnFailure)
            {
                _workspaceManager.DeleteWorkspace(workspace.JobRootPath);
            }

            throw;
        }
    }

    private void ValidateUploads(IReadOnlyList<PhaseOneArchiveUpload> archives)
    {
        if (archives.Count == 0)
        {
            throw new BadRequestException("At least one archive file is required.");
        }

        if (archives.Count > _options.MaxArchiveCount)
        {
            throw new LimitExceededException(
                $"The number of archives exceeds the limit of {_options.MaxArchiveCount}.");
        }

        long totalBytes = 0;
        foreach (var archive in archives)
        {
            if (string.IsNullOrWhiteSpace(archive.FileName))
            {
                throw new BadRequestException("Each archive must have a file name.");
            }

            var extension = Path.GetExtension(archive.FileName);
            if (!_options.AllowedArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    $"File '{archive.FileName}' is not an allowed archive type. Allowed extensions: {string.Join(", ", _options.AllowedArchiveExtensions)}.");
            }

            if (archive.Length <= 0)
            {
                throw new BadRequestException($"Archive '{archive.FileName}' is empty.");
            }

            if (archive.Length > _options.MaxArchiveSizeBytes)
            {
                throw new LimitExceededException(
                    $"Archive '{archive.FileName}' exceeds the maximum archive size of {_options.MaxArchiveSizeBytes} bytes.");
            }

            totalBytes += archive.Length;
        }

        if (totalBytes > _options.MaxTotalUploadedBytes)
        {
            throw new LimitExceededException(
                $"The total size of uploaded archives exceeds the limit of {_options.MaxTotalUploadedBytes} bytes.");
        }
    }

    private static string SanitizeSafeName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var builder = new StringBuilder(name.Length);

        foreach (var ch in name)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-');
        }

        var result = builder.ToString().Trim('-', '_', '.');
        return result.Length == 0 ? "archive" : result;
    }

    private static string GetUniqueSafeName(string baseName, HashSet<string> usedNames)
    {
        var candidate = baseName;
        var counter = 2;

        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName}-{counter}";
            counter++;
        }

        return candidate;
    }

    private List<Dtos.DetectedProjectInfo> DiscoverProjectRoots(string sourcePath)
    {
        try
        {
            var manifests = new List<Models.SourceFile>();
            foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file).ToLowerInvariant();
                if (name is not ("package.json") && !name.EndsWith(".csproj"))
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
                manifests.Add(new Models.SourceFile { RelativePath = relative, Content = content });
                if (manifests.Count >= 50)
                {
                    break;
                }
            }

            return _rootDiscoverer.Discover(manifests)
                .Select(r => new Dtos.DetectedProjectInfo { Root = r.Root, Framework = r.Framework, Language = r.Language })
                .ToList();
        }
        catch
        {
            return new List<Dtos.DetectedProjectInfo>();
        }
    }

    private static List<DetectedStack> AggregateStacks(IEnumerable<DetectedStack> stacks)
    {
        var result = new List<DetectedStack>();
        var byName = new Dictionary<string, DetectedStack>(StringComparer.OrdinalIgnoreCase);

        foreach (var stack in stacks)
        {
            if (byName.TryGetValue(stack.Name, out var existing))
            {
                foreach (var evidence in stack.Evidence)
                {
                    if (!existing.Evidence.Contains(evidence, StringComparer.OrdinalIgnoreCase))
                    {
                        existing.Evidence.Add(evidence);
                    }
                }
            }
            else
            {
                var copy = new DetectedStack
                {
                    Name = stack.Name,
                    Evidence = new List<string>(stack.Evidence),
                };

                byName[stack.Name] = copy;
                result.Add(copy);
            }
        }

        return result;
    }
}
