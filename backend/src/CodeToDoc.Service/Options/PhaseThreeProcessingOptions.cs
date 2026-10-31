namespace CodeToDoc.Service.Options;

public class PhaseThreeProcessingOptions
{
    public const string SectionName = "PhaseThreeProcessingOptions";

    public long MaxSourceFileBytes { get; set; } = 1_000_000;

    public int MaxAnalyzedFiles { get; set; } = 5_000;

    public int MaxComponentDepth { get; set; } = 10;

    public int MaxWorkBudget { get; set; } = 100_000;

    public int MaxExtractedElements { get; set; } = 10_000;
}
