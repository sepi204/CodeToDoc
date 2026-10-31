using CodeToDoc.Service.Services.Documents;

namespace CodeToDoc.Service.Interfaces;

public interface IMarkdownParser
{
    MarkdownDocument Parse(string markdown);
}
