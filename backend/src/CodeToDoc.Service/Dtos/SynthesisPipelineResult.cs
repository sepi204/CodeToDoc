using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Dtos;

public class SynthesisPipelineResult
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public PhaseOneResult Phase1 { get; set; } = new();

    public TechnicalSkeleton TechnicalSkeleton { get; set; } = new();

    public UiSkeleton UiSkeleton { get; set; } = new();

    public CorrelationSkeleton CorrelationSkeleton { get; set; } = new();

    public SynthesisManifest Synthesis { get; set; } = new();

    public SynthesisArtifacts Artifacts { get; set; } = new();
}

public class SynthesisArtifacts
{
    public string TechnicalSkeleton { get; set; } = "technical_skeleton.json";

    public string UiSkeleton { get; set; } = "ui_skeleton.json";

    public string CorrelationSkeleton { get; set; } = "correlation_skeleton.json";

    public string TechnicalDocument { get; set; } = "technical_document.md";

    public string UserGuide { get; set; } = "user_guide.md";

    public string Manifest { get; set; } = "synthesis_manifest.json";
}
