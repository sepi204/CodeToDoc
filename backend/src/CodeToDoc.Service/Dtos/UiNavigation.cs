namespace CodeToDoc.Service.Dtos;

public class NavigationItem
{
    public string Id { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string? Target { get; set; }

    public List<NavigationItem> Children { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}

public class SharedComponent
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public List<UiForm> Forms { get; set; } = new();

    public List<UiField> Fields { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}
