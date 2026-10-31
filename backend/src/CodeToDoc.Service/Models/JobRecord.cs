using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

/// <summary>
/// Immutable snapshot of a job's lifecycle, phases and compiled artifacts.
/// Repeated reads must never mutate this record.
/// </summary>
public sealed class JobRecord
{
    public string JobId { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public JobPhase Phase { get; set; } = JobPhase.Queued;

    public string ProgressMessage { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public List<JobPhaseResult> Phases { get; set; } = new();

    public List<ArtifactFileEntry> Artifacts { get; set; } = new();

    /// <summary>Safe, human-readable diagnostics only (no paths or secrets).</summary>
    public List<string> Diagnostics { get; set; } = new();
}

public sealed class JobPhaseResult
{
    public string Phase { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
