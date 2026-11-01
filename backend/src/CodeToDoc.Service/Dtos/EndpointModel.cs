namespace CodeToDoc.Service.Dtos;

public class EndpointModel
{
    public string Path { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string? ControllerName { get; set; }

    public string? ActionName { get; set; }

    public bool AuthRequired { get; set; }

    public List<string> Roles { get; set; } = new();

    public DtoModel? RequestDto { get; set; }

    public ResponseModel? ResponseDto { get; set; }

    public string? SourceProject { get; set; }
}
