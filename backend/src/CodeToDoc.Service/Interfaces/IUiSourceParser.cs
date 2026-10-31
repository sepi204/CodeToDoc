using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IUiSourceParser
{
    ParsedUiSource Parse(SourceFile file, FrontendProject project);
}
