namespace CodeToDoc.Service.Dtos;

/// <summary>Markdown preview payload for the document delivery UI.</summary>
public class DocumentPreviewResponse
{
    public string DocumentType { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public string Content { get; set; } = string.Empty;

    public List<string> AvailableFormats { get; set; } = new();

    public string Status { get; set; } = "notApplicable";
}
