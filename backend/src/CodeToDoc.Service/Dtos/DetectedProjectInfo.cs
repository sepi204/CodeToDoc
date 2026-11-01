namespace CodeToDoc.Service.Dtos;

public class DetectedProjectInfo
{
    public string Root { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string? Language { get; set; }
}
