namespace CodeToDoc.Service.Dtos;

public class PropertyModel
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public List<string> Rules { get; set; } = new();
}
