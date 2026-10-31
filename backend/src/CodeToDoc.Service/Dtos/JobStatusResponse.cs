using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Dtos;

/// <summary>Safe job-status response (no absolute paths or secrets).</summary>
public class JobStatusResponse
{
    public string JobId { get; set; } = string.Empty;

    public JobStatus Status { get; set; }

    public JobPhase Phase { get; set; }

    public string ProgressMessage { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public List<JobPhaseResult> Phases { get; set; } = new();

    public List<ArtifactFileEntry> Artifacts { get; set; } = new();

    public List<string> Diagnostics { get; set; } = new();

    public static JobStatusResponse From(JobRecord record) => new()
    {
        JobId = record.JobId,
        Status = record.Status,
        Phase = record.Phase,
        ProgressMessage = record.ProgressMessage,
        CreatedAtUtc = record.CreatedAtUtc,
        CompletedAtUtc = record.CompletedAtUtc,
        Phases = record.Phases,
        Artifacts = record.Artifacts,
        Diagnostics = record.Diagnostics,
    };
}
