namespace CodeToDoc.Service.Dtos;

public class CorrelationFieldMapping
{
    public List<FieldMapping> Mappings { get; set; } = new();

    public List<MissingRequiredField> MissingRequiredOnUi { get; set; } = new();

    public List<string> RequiredOnBackendButNotMarkedRequiredOnUi { get; set; } = new();
}

public class FieldMapping
{
    public string UiFieldName { get; set; } = string.Empty;

    public string DtoProperty { get; set; } = string.Empty;

    public string MappingKind { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}

public class MissingRequiredField
{
    public string DtoProperty { get; set; } = string.Empty;

    public bool IsRequiredOnBackend { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
