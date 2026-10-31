namespace CodeToDoc.Service.Dtos;

/// <summary>Response for job start (queued asynchronously).</summary>
public class JobStartResponse
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = "queued";
}
