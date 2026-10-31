using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class FullPipelineOrchestrator : IFullPipelineOrchestrator
{
    private readonly IPhaseOneProcessor _phaseOne;
    private readonly IPhaseTwoProcessor _phaseTwo;
    private readonly IPhaseThreeProcessor _phaseThree;
    private readonly ILogger<FullPipelineOrchestrator> _logger;

    public FullPipelineOrchestrator(
        IPhaseOneProcessor phaseOne,
        IPhaseTwoProcessor phaseTwo,
        IPhaseThreeProcessor phaseThree,
        ILogger<FullPipelineOrchestrator> logger)
    {
        _phaseOne = phaseOne;
        _phaseTwo = phaseTwo;
        _phaseThree = phaseThree;
        _logger = logger;
    }

    public async Task<FullPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        var phaseOneOutcome = await _phaseOne.ProcessWithWorkspaceAsync(archives, cancellationToken);
        var workspace = phaseOneOutcome.Workspace;

        _logger.LogInformation("Phase 1 completed for job {JobId}; running Phase 2 and Phase 3.",
            workspace.JobId);

        var technicalSkeleton = await _phaseTwo.ProcessAsync(workspace, cancellationToken);
        var uiSkeleton = await _phaseThree.ProcessAsync(workspace, cancellationToken);

        var status = uiSkeleton.Status == UiStatus.Partial ? "partial" : "complete";

        return new FullPipelineResult
        {
            JobId = workspace.JobId,
            Status = status,
            Phase1 = phaseOneOutcome.Result,
            TechnicalSkeleton = technicalSkeleton,
            UiSkeleton = uiSkeleton,
            Artifacts = new PipelineArtifacts(),
        };
    }
}
