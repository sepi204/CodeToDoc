using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Runs the real Phase 1-6 pipeline for a single job, reporting progress
/// through the job status store after each phase.
/// </summary>
public sealed class DocumentPipelineService : IDocumentPipelineService
{
    private readonly IPhaseOneProcessor _phaseOne;
    private readonly IPhaseTwoProcessor _phaseTwo;
    private readonly IPhaseThreeProcessor _phaseThree;
    private readonly IPhaseFourProcessor _phaseFour;
    private readonly IPhaseFiveProcessor _phaseFive;
    private readonly IPhaseSixProcessor _phaseSix;
    private readonly IJobStatusStore _statusStore;
    private readonly ILogger<DocumentPipelineService> _logger;

    public DocumentPipelineService(
        IPhaseOneProcessor phaseOne,
        IPhaseTwoProcessor phaseTwo,
        IPhaseThreeProcessor phaseThree,
        IPhaseFourProcessor phaseFour,
        IPhaseFiveProcessor phaseFive,
        IPhaseSixProcessor phaseSix,
        IJobStatusStore statusStore,
        ILogger<DocumentPipelineService> logger)
    {
        _phaseOne = phaseOne;
        _phaseTwo = phaseTwo;
        _phaseThree = phaseThree;
        _phaseFour = phaseFour;
        _phaseFive = phaseFive;
        _phaseSix = phaseSix;
        _statusStore = statusStore;
        _logger = logger;
    }

    public async Task RunAsync(JobRequest job, CancellationToken cancellationToken = default)
    {
        SetPhase(job.JobId, JobPhase.Phase1, "Ingesting and sanitizing archives");

        var uploads = job.Archives
            .Select(a => new PhaseOneArchiveUpload
            {
                FileName = a.FileName,
                Length = a.Content.Length,
                Stream = new MemoryStream(a.Content),
            })
            .ToList();

        try
        {
            var phaseOneOutcome = await _phaseOne.ProcessWithWorkspaceAsync(uploads, job.JobId, cancellationToken);
            var workspace = phaseOneOutcome.Workspace;
            RecordPhase(job.JobId, "phase1",
                phaseOneOutcome.Result.Status == ProcessingStatus.Succeeded ? "complete" : "failed");

            SetPhase(job.JobId, JobPhase.Phase2, "Running technical analysis");
            var technical = await _phaseTwo.ProcessAsync(workspace, cancellationToken);
            RecordPhase(job.JobId, "phase2", technical.Endpoints.Count > 0 ? "complete" : "notApplicable");

            SetPhase(job.JobId, JobPhase.Phase3, "Analyzing UI/UX");
            var ui = await _phaseThree.ProcessAsync(workspace, cancellationToken);
            RecordPhase(job.JobId, "phase3", Camel(ui.Status.ToString()));

            SetPhase(job.JobId, JobPhase.Phase4, "Correlating frontend and backend");
            var correlation = await _phaseFour.ProcessAsync(workspace, cancellationToken);
            RecordPhase(job.JobId, "phase4", Camel(correlation.Status.ToString()));

            SetPhase(job.JobId, JobPhase.Phase5, "Synthesizing documents");
            var synthesis = await _phaseFive.ProcessAsync(workspace, cancellationToken);
            RecordPhase(job.JobId, "phase5", Camel(synthesis.Status.ToString()));

            SetPhase(job.JobId, JobPhase.Rendering, "Compiling PDF and DOCX");
            var manifest = await _phaseSix.ProcessAsync(workspace, cancellationToken);

            SetPhase(job.JobId, JobPhase.Storing, "Publishing artifacts");
            RecordPhase(job.JobId, "phase6", manifest.Status);

            Complete(job.JobId, manifest);
        }
        finally
        {
            foreach (var upload in uploads)
            {
                upload.Stream.Dispose();
            }
        }
    }

    private void Complete(string jobId, ArtifactManifest manifest)
    {
        var record = _statusStore.Get(jobId);
        if (record is null)
        {
            return;
        }

        record.Artifacts = manifest.Files.ToList();
        record.Phase = JobPhase.Completed;
        record.ProgressMessage = "Complete";
        record.CompletedAtUtc = DateTime.UtcNow;

        var hasPartial = record.Phases.Any(p => p.Status is "partial" or "blocked")
            || manifest.Status is "partial" or "failed";

        record.Status = hasPartial ? JobStatus.CompletedWithWarnings : JobStatus.Completed;
        _statusStore.Upsert(record);
    }

    private void SetPhase(string jobId, JobPhase phase, string message)
    {
        var record = _statusStore.Get(jobId);
        if (record is null)
        {
            return;
        }

        record.Status = JobStatus.Running;
        record.Phase = phase;
        record.ProgressMessage = message;
        _statusStore.Upsert(record);
    }

    private void RecordPhase(string jobId, string phaseName, string status)
    {
        var record = _statusStore.Get(jobId);
        if (record is null)
        {
            return;
        }

        var existing = record.Phases.FirstOrDefault(p => p.Phase == phaseName);
        if (existing is null)
        {
            record.Phases.Add(new JobPhaseResult { Phase = phaseName, Status = status });
        }
        else
        {
            existing.Status = status;
        }

        _statusStore.Upsert(record);
    }

    private static string Camel(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
