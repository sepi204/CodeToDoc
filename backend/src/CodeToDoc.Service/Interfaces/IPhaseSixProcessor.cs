using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>
/// Phase 6: compiles the Phase 5 Markdown documents into PDF/DOCX, publishes
/// all artifacts through the artifact store, and writes the artifact manifest.
/// </summary>
public interface IPhaseSixProcessor
{
    Task<ArtifactManifest> ProcessAsync(JobWorkspace workspace, CancellationToken cancellationToken = default);
}
