using System.Globalization;
using System.Text;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;

namespace CodeToDoc.Service.Services;

public sealed class FileTreeBuilder : IFileTreeBuilder
{
    public FileTreeNode Build(string sourceDirectory)
    {
        var root = new FileTreeNode
        {
            Name = "source",
            RelativePath = string.Empty,
            NodeType = NodeType.Directory,
        };

        BuildDirectory(root, sourceDirectory);
        return root;
    }

    public string BuildText(FileTreeNode root)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatNode(root));
        AppendChildren(builder, root, string.Empty);
        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static void BuildDirectory(FileTreeNode node, string directory)
    {
        var directories = new List<string>();
        var files = new List<string>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                // Do not follow symbolic links or junctions.
                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                directories.Add(entry);
            }
            else
            {
                files.Add(entry);
            }
        }

        directories.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in directories)
        {
            var name = Path.GetFileName(dir);
            var child = new FileTreeNode
            {
                Name = name,
                RelativePath = JoinRelative(node.RelativePath, name),
                NodeType = NodeType.Directory,
            };

            BuildDirectory(child, dir);
            node.Children.Add(child);
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var info = new FileInfo(file);
            node.Children.Add(new FileTreeNode
            {
                Name = name,
                RelativePath = JoinRelative(node.RelativePath, name),
                NodeType = NodeType.File,
                Extension = Path.GetExtension(name),
                SizeBytes = info.Length,
            });
        }

        node.SizeBytes = node.Children.Sum(child => child.SizeBytes);
    }

    private static void AppendChildren(StringBuilder builder, FileTreeNode node, string prefix)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var isLast = i == node.Children.Count - 1;
            var branch = isLast ? "└── " : "├── ";

            builder.AppendLine(prefix + branch + FormatNode(child));

            if (child.Children.Count > 0)
            {
                AppendChildren(builder, child, prefix + (isLast ? "    " : "│   "));
            }
        }
    }

    private static string FormatNode(FileTreeNode node)
    {
        if (node.NodeType == NodeType.Directory)
        {
            return $"{node.Name}/ ({FormatBytes(node.SizeBytes)})";
        }

        return $"{node.Name} ({FormatBytes(node.SizeBytes)}, {node.Extension})";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{bytes} B";
        }

        return $"{size.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private static string JoinRelative(string parent, string name)
    {
        return string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
    }
}
