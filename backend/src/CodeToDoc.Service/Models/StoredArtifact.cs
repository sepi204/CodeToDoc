using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

/// <summary>Metadata for a single stored artifact (never exposes absolute paths).</summary>
public sealed class StoredArtifact
{
    public string ArtifactId { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public ArtifactFormat Format { get; set; }

    public ArtifactStatus Status { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? Checksum { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
