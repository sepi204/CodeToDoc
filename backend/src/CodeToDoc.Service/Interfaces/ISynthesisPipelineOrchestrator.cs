using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface ISynthesisPipelineOrchestrator
{
    Task<SynthesisPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);
}
