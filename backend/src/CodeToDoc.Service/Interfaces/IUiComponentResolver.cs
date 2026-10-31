using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IUiComponentResolver
{
    List<ParsedComponent> ResolveImports(
        SourceFile pageFile,
        ParsedUiSource source,
        FrontendProject project,
        IReadOnlyList<SourceFile> workspaceFiles,
        Action<UiDiagnostic> reportDiagnostic);
}
