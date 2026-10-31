using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly IPhaseOneProcessor _phaseOne;
    private readonly IPhaseTwoProcessor _phaseTwo;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        IPhaseOneProcessor phaseOne,
        IPhaseTwoProcessor phaseTwo,
        ILogger<PipelineOrchestrator> logger)
    {
        _phaseOne = phaseOne;
        _phaseTwo = phaseTwo;
        _logger = logger;
    }

    public async Task<TechnicalSkeleton> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        var phaseOneOutcome = await _phaseOne.ProcessWithWorkspaceAsync(archives, cancellationToken);

        _logger.LogInformation("Phase 1 completed for job {JobId}; starting Phase 2.",
            phaseOneOutcome.Workspace.JobId);

        return await _phaseTwo.ProcessAsync(phaseOneOutcome.Workspace, cancellationToken);
    }
}
