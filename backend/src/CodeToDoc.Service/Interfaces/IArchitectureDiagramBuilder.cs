using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

/// <summary>Builds an evidence-derived architecture diagram from validated skeletons.</summary>
public interface IArchitectureDiagramBuilder
{
    ArchitectureDiagram Build(TechnicalSkeleton technical, UiSkeleton? ui, CorrelationSkeleton? correlation);
}
