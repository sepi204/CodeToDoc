using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class PhaseOneResult
{
    public string JobId { get; set; } = string.Empty;

    public ProcessingStatus Status { get; set; }

    public int ArchiveCount { get; set; }

    public int TotalArchiveEntries { get; set; }

    public int ExtractedFileCount { get; set; }

    public int SkippedEntryCount { get; set; }

    public long TotalExtractedBytes { get; set; }

    public List<DetectedStack> AggregateDetectedStacks { get; set; } = new();

    public List<DetectedProjectInfo> ProjectRoots { get; set; } = new();

    public List<ArchiveResult> ArchiveResults { get; set; } = new();

    public FileTreeNode? FileTree { get; set; }

    public string TextFileTree { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = new();

    public long ProcessingDurationMs { get; set; }

    public long StackDetectionDurationMs { get; set; }
}
