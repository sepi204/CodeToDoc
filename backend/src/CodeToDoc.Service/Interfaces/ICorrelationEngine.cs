using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface ICorrelationEngine
{
    CorrelationSkeleton Correlate(
        TechnicalSkeleton technical,
        UiSkeleton ui,
        IReadOnlyList<SourceFile> sourceFiles,
        string jobId);
}
