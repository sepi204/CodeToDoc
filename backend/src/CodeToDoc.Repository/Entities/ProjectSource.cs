using CodeToDoc.Repository.Enums;

namespace CodeToDoc.Repository.Entities;

public class ProjectSource
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public SourceKind Kind { get; set; }

    public string? FileName { get; set; }

    public long? FileSize { get; set; }

    public string? ContentType { get; set; }

    public string? RepoUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
