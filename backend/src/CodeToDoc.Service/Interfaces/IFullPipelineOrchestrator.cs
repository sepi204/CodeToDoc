using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface IFullPipelineOrchestrator
{
    Task<FullPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);
}
