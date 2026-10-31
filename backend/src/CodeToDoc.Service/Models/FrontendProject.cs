namespace CodeToDoc.Service.Models;

public sealed class FrontendProject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Root { get; init; } = string.Empty;

    public string Framework { get; init; } = "unknown";

    public IReadOnlyList<SourceFile> SourceFiles { get; init; } = Array.Empty<SourceFile>();
}
