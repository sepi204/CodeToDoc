namespace CodeToDoc.Service.Models;

/// <summary>Input to a document renderer: validated Markdown plus metadata.</summary>
public sealed class DocumentRenderInput
{
    public string DocumentType { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>ISO language code ("en", "fa").</summary>
    public string Language { get; init; } = "en";

    public string Markdown { get; init; } = string.Empty;

    /// <summary>Optional path to a font file to embed (e.g. a Persian-capable TTF).</summary>
    public string? FontPath { get; init; }
}
