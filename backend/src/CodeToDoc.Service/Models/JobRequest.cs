namespace CodeToDoc.Service.Models;

/// <summary>A queued processing request for the full Phase 1-6 pipeline.</summary>
public sealed class JobRequest
{
    public string JobId { get; init; } = string.Empty;

    public IReadOnlyList<JobArchiveData> Archives { get; init; } = Array.Empty<JobArchiveData>();
}
