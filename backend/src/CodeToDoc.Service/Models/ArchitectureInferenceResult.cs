using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Models;

public sealed class ArchitectureInferenceResult
{
    public ArchitectureSummary Summary { get; init; } = new();

    public List<DependencyEntry> Dependencies { get; init; } = new();
}
