using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IArchiveExtractor
{
    Task<ArchiveExtractionResult> ExtractAsync(
        PhaseOneArchiveUpload archive,
        string sourceDirectory,
        string safeDirectoryName,
        ExtractionContext context,
        CancellationToken cancellationToken = default);
}
