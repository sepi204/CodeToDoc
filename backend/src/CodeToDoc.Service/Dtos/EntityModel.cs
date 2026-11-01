namespace CodeToDoc.Service.Dtos;

public class EntityModel
{
    public string Name { get; set; } = string.Empty;

    public List<PropertyModel> Properties { get; set; } = new();

    public string? Source { get; set; }
}
