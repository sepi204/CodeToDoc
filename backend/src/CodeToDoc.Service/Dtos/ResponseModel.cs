namespace CodeToDoc.Service.Dtos;

public class ResponseModel
{
    public string? TypeName { get; set; }

    public int StatusCode { get; set; }

    public List<PropertyModel> Properties { get; set; } = new();
}
