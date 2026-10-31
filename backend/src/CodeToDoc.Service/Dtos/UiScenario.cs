using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class UiScenario
{
    public string Id { get; set; } = string.Empty;

    public string? TriggerElementId { get; set; }

    public ActionKind Kind { get; set; }

    public string? HandlerExpression { get; set; }

    public string? NavigationTarget { get; set; }

    public List<UiApiReference> ApiReferences { get; set; } = new();

    public List<string> RequiredFieldIds { get; set; } = new();

    public string? UnresolvedBehavior { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
