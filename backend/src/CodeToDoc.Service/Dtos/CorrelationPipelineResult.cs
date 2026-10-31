namespace CodeToDoc.Service.Dtos;

public class CorrelationPipelineResult
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public PhaseOneResult Phase1 { get; set; } = new();

    public TechnicalSkeleton TechnicalSkeleton { get; set; } = new();

    public UiSkeleton UiSkeleton { get; set; } = new();

    public CorrelationSkeleton CorrelationSkeleton { get; set; } = new();

    public CorrelationArtifacts Artifacts { get; set; } = new();
}

public class CorrelationArtifacts
{
    public string TechnicalSkeleton { get; set; } = "technical_skeleton.json";

    public string UiSkeleton { get; set; } = "ui_skeleton.json";

    public string CorrelationSkeleton { get; set; } = "correlation_skeleton.json";
}
