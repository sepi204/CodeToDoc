using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface ICSharpAstParser
{
    List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles);
}
