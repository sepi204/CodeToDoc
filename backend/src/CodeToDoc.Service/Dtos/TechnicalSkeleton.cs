namespace CodeToDoc.Service.Dtos;

public class TechnicalSkeleton
{
    public string JobId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public ArchitectureSummary Summary { get; set; } = new();

    public List<DependencyEntry> Dependencies { get; set; } = new();

    public List<EndpointModel> Endpoints { get; set; } = new();

    public List<DetectedProjectInfo> Projects { get; set; } = new();

    public List<EntityModel> Entities { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}
