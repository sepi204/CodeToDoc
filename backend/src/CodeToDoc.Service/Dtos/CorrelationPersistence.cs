using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class CorrelationPersistence
{
    public PersistenceKind Kind { get; set; } = PersistenceKind.Unknown;

    public string? Operation { get; set; }

    public string? EntityType { get; set; }

    public List<string> EvidenceIds { get; set; } = new();
}
