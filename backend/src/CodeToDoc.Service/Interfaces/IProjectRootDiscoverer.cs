using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IProjectRootDiscoverer
{
    List<DetectedProjectRoot> Discover(IReadOnlyList<SourceFile> sourceFiles);
}
