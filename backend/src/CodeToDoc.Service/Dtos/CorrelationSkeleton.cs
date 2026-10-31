using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class CorrelationSkeleton
{
    public string SchemaVersion { get; set; } = "1.0";

    public string JobId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public CorrelationStatus Status { get; set; }

    public List<Correlation> Correlations { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class Correlation
{
    public string Id { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string PageId { get; set; } = string.Empty;

    public string ActionId { get; set; } = string.Empty;

    public string? ScenarioId { get; set; }

    public CorrelationUi Ui { get; set; } = new();

    public CorrelationBackend Backend { get; set; } = new();

    public CorrelationFieldMapping FieldMapping { get; set; } = new();

    public CorrelationPersistence Persistence { get; set; } = new();

    public double Confidence { get; set; }

    public List<string> MatchReasons { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class CorrelationUi
{
    public string? PageRoute { get; set; }

    public string? ActionLabel { get; set; }

    public ActionKind ActionKind { get; set; }

    public CorrelationApiReference ApiReference { get; set; } = new();
}

public class CorrelationApiReference
{
    public string? Method { get; set; }

    public string? Url { get; set; }

    public string? UrlExpression { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}

public class CorrelationBackend
{
    public string? EndpointId { get; set; }

    public string? Method { get; set; }

    public string? PathTemplate { get; set; }

    public string? ControllerName { get; set; }

    public string? ActionName { get; set; }

    public bool AuthRequired { get; set; }

    public List<string> Roles { get; set; } = new();

    public string? RequestDtoType { get; set; }

    public string? ResponseDtoType { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
