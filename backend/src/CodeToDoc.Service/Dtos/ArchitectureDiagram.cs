namespace CodeToDoc.Service.Dtos;

/// <summary>
/// Evidence-derived architecture diagram. Produced deterministically from
/// validated Phase 2-4 skeleton data only; never from LLM output.
/// </summary>
public class ArchitectureDiagram
{
    public bool Available { get; set; }

    public List<DiagramNode> Nodes { get; set; } = new();

    public List<DiagramEdge> Edges { get; set; } = new();

    /// <summary>Human-readable reason when <see cref="Available"/> is false.</summary>
    public string? UnavailableReason { get; set; }
}

public class DiagramNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>repository | page | endpoint | dependency</summary>
    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public class DiagramEdge
{
    public string FromId { get; set; } = string.Empty;

    public string ToId { get; set; } = string.Empty;

    /// <summary>contains | correlates | dependsOn</summary>
    public string Kind { get; set; } = string.Empty;

    public bool Inferred { get; set; }
}
