namespace CodeToDoc.Service.Dtos;

public class FullPipelineResult
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public PhaseOneResult Phase1 { get; set; } = new();

    public TechnicalSkeleton TechnicalSkeleton { get; set; } = new();

    public UiSkeleton UiSkeleton { get; set; } = new();

    public PipelineArtifacts Artifacts { get; set; } = new();
}

public class PipelineArtifacts
{
    public string TechnicalSkeleton { get; set; } = "technical_skeleton.json";

    public string UiSkeleton { get; set; } = "ui_skeleton.json";
}
