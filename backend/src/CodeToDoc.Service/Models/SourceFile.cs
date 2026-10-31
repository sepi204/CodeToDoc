namespace CodeToDoc.Service.Models;

public sealed class SourceFile
{
    public string RelativePath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}
