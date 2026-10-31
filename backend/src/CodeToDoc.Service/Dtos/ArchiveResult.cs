namespace CodeToDoc.Service.Dtos;

public class ArchiveResult
{
    public string OriginalFileName { get; set; } = string.Empty;

    public string SafeDirectoryName { get; set; } = string.Empty;

    public int TotalEntries { get; set; }

    public int ExtractedFileCount { get; set; }

    public int SkippedEntryCount { get; set; }

    public long ExtractedBytes { get; set; }

    public List<DetectedStack> DetectedStacks { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}
