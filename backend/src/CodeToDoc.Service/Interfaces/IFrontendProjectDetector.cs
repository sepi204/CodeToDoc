using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IFrontendProjectDetector
{
    List<FrontendProject> Detect(string sourcePath, IReadOnlyList<SourceFile> sourceFiles);
}
