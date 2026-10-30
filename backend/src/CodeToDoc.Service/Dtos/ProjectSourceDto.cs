using CodeToDoc.Repository.Enums;

namespace CodeToDoc.Service.Dtos;

public class ProjectSourceDto
{
    public Guid Id { get; set; }

    public SourceKind Kind { get; set; }

    public string? Name { get; set; }

    public long? Size { get; set; }

    public string? ContentType { get; set; }

    public string? Url { get; set; }
}
