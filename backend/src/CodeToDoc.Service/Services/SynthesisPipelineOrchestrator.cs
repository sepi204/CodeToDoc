using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

public sealed class SynthesisPipelineOrchestrator : ISynthesisPipelineOrchestrator
{
    private readonly IPhaseOneProcessor _phaseOne;
    private readonly IPhaseTwoProcessor _phaseTwo;
    private readonly IPhaseThreeProcessor _phaseThree;
    private readonly IPhaseFourProcessor _phaseFour;
    private readonly IPhaseFiveProcessor _phaseFive;
    private readonly ILogger<SynthesisPipelineOrchestrator> _logger;

    public SynthesisPipelineOrchestrator(
        IPhaseOneProcessor phaseOne,
        IPhaseTwoProcessor phaseTwo,
        IPhaseThreeProcessor phaseThree,
        IPhaseFourProcessor phaseFour,
        IPhaseFiveProcessor phaseFive,
        ILogger<SynthesisPipelineOrchestrator> logger)
    {
        _phaseOne = phaseOne;
        _phaseTwo = phaseTwo;
        _phaseThree = phaseThree;
        _phaseFour = phaseFour;
        _phaseFive = phaseFive;
        _logger = logger;
    }

    public async Task<SynthesisPipelineResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default)
    {
        var phaseOneOutcome = await _phaseOne.ProcessWithWorkspaceAsync(archives, cancellationToken);
        var workspace = phaseOneOutcome.Workspace;

        _logger.LogInformation("Phase 1 completed for job {JobId}; running Phases 2-5.", workspace.JobId);

        var technical = await _phaseTwo.ProcessAsync(workspace, cancellationToken);
        var ui = await _phaseThree.ProcessAsync(workspace, cancellationToken);
        var correlation = await _phaseFour.ProcessAsync(workspace, cancellationToken);
        var synthesis = await _phaseFive.ProcessAsync(workspace, cancellationToken);

        var status = synthesis.Status switch
        {
            SynthesisStatus.Complete or SynthesisStatus.NotApplicable => "complete",
            _ => "partial",
        };

        return new SynthesisPipelineResult
        {
            JobId = workspace.JobId,
            Status = status,
            Phase1 = phaseOneOutcome.Result,
            TechnicalSkeleton = technical,
            UiSkeleton = ui,
            CorrelationSkeleton = correlation,
            Synthesis = synthesis,
            Artifacts = new SynthesisArtifacts(),
        };
    }
}
