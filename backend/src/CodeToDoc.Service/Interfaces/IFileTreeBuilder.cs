using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Interfaces;

public interface IFileTreeBuilder
{
    FileTreeNode Build(string sourceDirectory);

    string BuildText(FileTreeNode root);
}
