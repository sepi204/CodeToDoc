namespace CodeToDoc.Service.Dtos;

public class DependencyEntry
{
    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string Category { get; set; } = string.Empty;
}
