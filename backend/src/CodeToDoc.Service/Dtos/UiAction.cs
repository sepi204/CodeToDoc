using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class UiAction
{
    public string Id { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string ElementType { get; set; } = string.Empty;

    public ActionKind Kind { get; set; }

    public string? HandlerExpression { get; set; }

    public string? FormId { get; set; }

    public string? NavigationTarget { get; set; }

    public List<UiApiReference> ApiReferences { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiApiReference
{
    public string? Method { get; set; }

    public string? Url { get; set; }

    public string? UrlExpression { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
