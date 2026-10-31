using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Dtos;

public class FileTreeNode
{
    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public NodeType NodeType { get; set; }

    public string? Extension { get; set; }

    public long SizeBytes { get; set; }

    public List<FileTreeNode> Children { get; set; } = new();
}
