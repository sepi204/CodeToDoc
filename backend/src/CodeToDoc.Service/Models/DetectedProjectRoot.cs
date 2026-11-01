namespace CodeToDoc.Service.Models;

public sealed class DetectedProjectRoot
{
    public string Root { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public string Framework { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public List<string> Evidence { get; init; } = new();
}
