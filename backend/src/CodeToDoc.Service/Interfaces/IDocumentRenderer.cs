using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>Renders a validated Markdown document into a specific output format.</summary>
public interface IDocumentRenderer
{
    /// <summary>Output format identifier ("pdf" or "docx").</summary>
    string Format { get; }

    byte[] Render(DocumentRenderInput input, CancellationToken cancellationToken = default);
}
