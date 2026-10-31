using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class SynthesisManifest
{
    public string SchemaVersion { get; set; } = "1.0";

    public string JobId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public SynthesisStatus Status { get; set; }

    public List<SynthesisDocumentEntry> Documents { get; set; } = new();

    public SynthesisMeasurements Measurements { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class SynthesisDocumentEntry
{
    public string DocumentType { get; set; } = string.Empty;

    public SynthesisStatus Status { get; set; }

    public string Artifact { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public List<string> TemplateVersions { get; set; } = new();

    public string Model { get; set; } = string.Empty;

    public string InputFingerprint { get; set; } = string.Empty;

    public int ModuleCount { get; set; }

    public int RequestCount { get; set; }

    public int RetryCount { get; set; }

    public SynthesisUsage Usage { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class SynthesisUsage
{
    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public TokenCountKind TokenCountKind { get; set; }
}

public class SynthesisMeasurements
{
    public long SanitizedSourceBytes { get; set; }

    public long SkeletonInputBytes { get; set; }

    public long ProviderPayloadBytes { get; set; }

    public double? EstimatedReductionPercent { get; set; }
}
