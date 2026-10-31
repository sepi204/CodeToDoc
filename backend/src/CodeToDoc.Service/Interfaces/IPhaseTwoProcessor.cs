using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPhaseTwoProcessor
{
    Task<TechnicalSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default);
}
