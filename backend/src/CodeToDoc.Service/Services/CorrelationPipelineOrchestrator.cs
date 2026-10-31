using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class CorrelationPipelineOrchestrator : ICorrelationPipelineOrchestrator
{
    private readonly IPhaseOneProcessor _phaseOne;
    private readonly IPhaseTwoProcessor _phaseTwo;
    private readonly IPhaseThreeProcessor _phaseThree;
    private readonly IPhaseFourProcessor _phaseFour;
    private readonly ILogger<CorrelationPipelineOrchestrator> _logger;

    public CorrelationPipelineOrchestrator(
        IPhaseOneProcessor phaseOne,
        IPhaseTwoProcessor phaseTwo,
        IPhaseThreeProcessor phaseThree,
        IPhaseFourProcessor phaseFour,
        ILogger<CorrelationPipelineOrchestrator> logger)
    {
        _phaseOne = phaseOne;
        _phaseTwo = phaseTwo;
        _phaseThree = phaseThree;
        _phaseFour = phaseFour;
        _logger = logger;
    }

    public async Task<CorrelationPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        var phaseOneOutcome = await _phaseOne.ProcessWithWorkspaceAsync(archives, cancellationToken);
        var workspace = phaseOneOutcome.Workspace;

        _logger.LogInformation("Phase 1 completed for job {JobId}; running Phases 2, 3 and 4.",
            workspace.JobId);

        var technical = await _phaseTwo.ProcessAsync(workspace, cancellationToken);
        var ui = await _phaseThree.ProcessAsync(workspace, cancellationToken);
        var correlation = await _phaseFour.ProcessAsync(workspace, cancellationToken);

        var status = correlation.Status is CorrelationStatus.Partial or CorrelationStatus.Blocked
            ? "partial"
            : "complete";

        return new CorrelationPipelineResult
        {
            JobId = workspace.JobId,
            Status = status,
            Phase1 = phaseOneOutcome.Result,
            TechnicalSkeleton = technical,
            UiSkeleton = ui,
            CorrelationSkeleton = correlation,
            Artifacts = new CorrelationArtifacts(),
        };
    }
}
