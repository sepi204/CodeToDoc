namespace CodeToDoc.Service.Models;

public sealed class ParsedDependency
{
    public string Name { get; init; } = string.Empty;

    public string? Version { get; init; }

    public string Source { get; init; } = string.Empty;
}
