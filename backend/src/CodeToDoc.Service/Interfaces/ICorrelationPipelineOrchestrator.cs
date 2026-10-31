using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface ICorrelationPipelineOrchestrator
{
    Task<CorrelationPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);
}
