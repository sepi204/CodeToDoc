namespace CodeToDoc.Service.Models;

public sealed class DiscoveredRoute
{
    public string RepositoryId { get; init; } = string.Empty;

    public string SourceFile { get; init; } = string.Empty;

    public string? Route { get; init; }

    public string? RouteExpression { get; init; }

    public List<string> RouteParameters { get; init; } = new();

    public bool IsResolved { get; init; }
}
