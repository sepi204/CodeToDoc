namespace CodeToDoc.Service.Dtos;

public class UiTable
{
    public string Id { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public List<UiTableColumn> Columns { get; set; } = new();

    public List<UiAction> RowActions { get; set; } = new();

    public List<string> EvidenceIds { get; set; } = new();
}

public class UiTableColumn
{
    public string? Label { get; set; }

    public string? Accessor { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
