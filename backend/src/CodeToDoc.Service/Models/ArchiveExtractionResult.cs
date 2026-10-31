using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Models;

public sealed class ArchiveExtractionResult
{
    public string OriginalFileName { get; set; } = string.Empty;

    public int TotalEntries { get; set; }

    public int ExtractedFileCount { get; set; }

    public int SkippedEntryCount { get; set; }

    public long ExtractedBytes { get; set; }

    public List<DetectedStack> DetectedStacks { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public long StackDetectionMs { get; set; }
}
