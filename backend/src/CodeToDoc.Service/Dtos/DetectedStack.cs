namespace CodeToDoc.Service.Dtos;

public class DetectedStack
{
    public string Name { get; set; } = string.Empty;

    public List<string> Evidence { get; set; } = new();
}
