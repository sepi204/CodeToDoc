namespace CodeToDoc.Service.Dtos;

public class UiPage
{
    public string Id { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string? Route { get; set; }

    public string? RouteExpression { get; set; }

    public string? Title { get; set; }

    public string SourceFile { get; set; } = string.Empty;

    public List<string> RouteParameters { get; set; } = new();

    public List<UiForm> Forms { get; set; } = new();

    public List<UiAction> Actions { get; set; } = new();

    public List<UiTable> Tables { get; set; } = new();

    public List<UiScenario> Scenarios { get; set; } = new();

    public List<UiPage> Children { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}
