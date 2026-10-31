namespace CodeToDoc.Service.Models;

/// <summary>An uploaded archive payload held in memory for asynchronous processing.</summary>
public sealed class JobArchiveData
{
    public string FileName { get; init; } = string.Empty;

    public byte[] Content { get; init; } = Array.Empty<byte>();
}
