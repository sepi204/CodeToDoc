using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPhaseThreeProcessor
{
    Task<UiSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default);
}
