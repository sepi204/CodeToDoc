using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPhaseFiveProcessor
{
    Task<SynthesisManifest> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default);
}
