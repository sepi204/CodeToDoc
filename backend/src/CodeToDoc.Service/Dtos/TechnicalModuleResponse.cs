namespace CodeToDoc.Service.Dtos;

public class TechnicalModuleResponse
{
    public string SchemaVersion { get; set; } = "1.0";

    public string DocumentType { get; set; } = "technical";

    public string ModuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<SynthesisStatement> Overview { get; set; } = new();

    public List<SynthesisStatement> Technologies { get; set; } = new();

    public List<SynthesisStatement> Architecture { get; set; } = new();

    public List<SynthesisStatement> Authentication { get; set; } = new();

    public List<SynthesisStatement> Prerequisites { get; set; } = new();

    public List<TechnicalEndpointSection> Endpoints { get; set; } = new();

    public List<string> Limitations { get; set; } = new();
}

public class SynthesisStatement
{
    public string Text { get; set; } = string.Empty;

    public string Certainty { get; set; } = "unknown";

    public List<string> EvidenceIds { get; set; } = new();
}

public class TechnicalEndpointSection
{
    public string EndpointId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Certainty { get; set; } = "explicit";

    public string? RequestExample { get; set; }

    public string? ResponseExample { get; set; }

    public List<string> Notes { get; set; } = new();
}
