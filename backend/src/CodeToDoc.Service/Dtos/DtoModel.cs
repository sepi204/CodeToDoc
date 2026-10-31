namespace CodeToDoc.Service.Dtos;

public class DtoModel
{
    public string TypeName { get; set; } = string.Empty;

    public List<PropertyModel> Properties { get; set; } = new();
}
