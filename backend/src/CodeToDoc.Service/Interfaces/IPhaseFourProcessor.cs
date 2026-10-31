using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPhaseFourProcessor
{
    Task<CorrelationSkeleton> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default);
}
