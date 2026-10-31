using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPhaseOneProcessor
{
    Task<PhaseOneResult> ProcessAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);

    Task<PhaseOneWorkspaceResult> ProcessWithWorkspaceAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        CancellationToken cancellationToken = default);

    Task<PhaseOneWorkspaceResult> ProcessWithWorkspaceAsync(
        IReadOnlyList<PhaseOneArchiveUpload> archives,
        string jobId,
        CancellationToken cancellationToken = default);
}
