using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class UiSkeleton
{
    public string SchemaVersion { get; set; } = "1.0";

    public string JobId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public UiStatus Status { get; set; }

    public List<UiRepository> Repositories { get; set; } = new();

    public List<UiPage> Pages { get; set; } = new();

    public List<NavigationItem> Navigation { get; set; } = new();

    public List<SharedComponent> SharedComponents { get; set; } = new();

    public List<UiEvidence> Evidence { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class UiRepository
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Root { get; set; } = string.Empty;

    public string Framework { get; set; } = "unknown";
}
