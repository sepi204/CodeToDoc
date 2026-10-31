using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

public sealed class ExtractionContext
{
    public int TotalArchiveEntries { get; set; }

    public int TotalExtractedFileCount { get; set; }

    public long TotalExtractedBytes { get; set; }

    public int TotalSkippedEntries { get; set; }

    public Dictionary<SkipReason, int> SkipReasonCounts { get; } = new();

    public void RecordSkip(SkipReason reason)
    {
        TotalSkippedEntries++;
        SkipReasonCounts[reason] = SkipReasonCounts.GetValueOrDefault(reason) + 1;
    }
}
