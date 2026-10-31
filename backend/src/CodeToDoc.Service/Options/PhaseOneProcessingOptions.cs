namespace CodeToDoc.Service.Options;

public class PhaseOneProcessingOptions
{
    public const string SectionName = "PhaseOneProcessingOptions";

    public string TempRootPath { get; set; } = string.Empty;

    public int MaxArchiveCount { get; set; } = 5;

    public long MaxArchiveSizeBytes { get; set; } = 100L * 1024 * 1024;

    public long MaxTotalUploadedBytes { get; set; } = 250L * 1024 * 1024;

    public int MaxEntryCount { get; set; } = 100_000;

    public int MaxExtractedFileCount { get; set; } = 50_000;

    public long MaxSingleExtractedFileBytes { get; set; } = 20L * 1024 * 1024;

    public long MaxTotalExtractedBytes { get; set; } = 500L * 1024 * 1024;

    public int MaxCompressionRatio { get; set; } = 100;

    public int MaxPathLength { get; set; } = 1024;

    public bool DeleteJobDirectoryOnFailure { get; set; } = true;

    public List<string> AllowedArchiveExtensions { get; set; } = new() { ".zip" };
}
