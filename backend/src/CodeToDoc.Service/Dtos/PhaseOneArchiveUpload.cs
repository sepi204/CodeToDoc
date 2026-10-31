namespace CodeToDoc.Service.Dtos;

public sealed class PhaseOneArchiveUpload
{
    public string FileName { get; set; } = string.Empty;

    public long Length { get; set; }

    public Stream Stream { get; set; } = Stream.Null;
}
