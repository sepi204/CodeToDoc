using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface IPipelineOrchestrator
{
    Task<TechnicalSkeleton> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);
}
