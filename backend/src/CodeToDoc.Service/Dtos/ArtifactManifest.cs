using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

/// <summary>Phase 6 artifact manifest describing every compiled file.</summary>
public class ArtifactManifest
{
    public string SchemaVersion { get; set; } = "1.0";

    public string JobId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    public string Status { get; set; } = "complete";

    public List<ArtifactFileEntry> Files { get; set; } = new();

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}

public class ArtifactFileEntry
{
    public string DocumentType { get; set; } = string.Empty;

    public ArtifactFormat Format { get; set; }

    public ArtifactStatus Status { get; set; }

    /// <summary>Safe artifact identifier (relative filename, no path separators).</summary>
    public string ArtifactId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? Checksum { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<UiDiagnostic> Diagnostics { get; set; } = new();
}
