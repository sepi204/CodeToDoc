using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class UiEvidence
{
    public string Id { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    public EvidenceKind Kind { get; set; }
}

public class UiDiagnostic
{
    public string Code { get; set; } = string.Empty;

    public DiagnosticSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public UiSourceReference? SourceReference { get; set; }
}

public class UiSourceReference
{
    public string? FilePath { get; set; }

    public int? StartLine { get; set; }

    public int? EndLine { get; set; }
}
