using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>Runs the full Phase 1-6 pipeline for a single queued job.</summary>
public interface IDocumentPipelineService
{
    Task RunAsync(JobRequest job, CancellationToken cancellationToken = default);
}
