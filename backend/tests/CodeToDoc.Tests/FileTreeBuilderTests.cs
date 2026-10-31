using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class FileTreeBuilderTests : IDisposable
{
    private readonly string _root = TestHelpers.CreateTempRoot();
    private readonly FileTreeBuilder _builder = new();

    public void Dispose() => TestHelpers.DeleteDirectory(_root);

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void BuildsStructuredTree()
    {
        WriteFile("backend/Backend.csproj", "<Project />");
        WriteFile("backend/Program.cs", "Console.WriteLine();");
        WriteFile("frontend/package.json", "{}");

        var tree = _builder.Build(_root);

        Assert.Equal(NodeType.Directory, tree.NodeType);
        Assert.Equal("source", tree.Name);

        var backend = tree.Children.Single(c => c.Name == "backend");
        Assert.Equal(NodeType.Directory, backend.NodeType);
        Assert.Equal("backend", backend.RelativePath);

        var program = backend.Children.Single(c => c.Name == "Program.cs");
        Assert.Equal(NodeType.File, program.NodeType);
        Assert.Equal("backend/Program.cs", program.RelativePath);
        Assert.Equal(".cs", program.Extension);
    }

    [Fact]
    public void ProducesDeterministicTextualTree()
    {
        WriteFile("backend/Backend.csproj", "<Project />");
        WriteFile("backend/Program.cs", "x");
        WriteFile("frontend/package.json", "{}");

        var tree = _builder.Build(_root);
        var text1 = _builder.BuildText(tree);
        var text2 = _builder.BuildText(_builder.Build(_root));

        Assert.Equal(text1, text2);
        Assert.StartsWith("source/", text1);
        Assert.Contains("Backend.csproj", text1);
        Assert.Contains("Program.cs", text1);
        Assert.Contains("package.json", text1);
    }

    [Fact]
    public void SortsDirectoriesBeforeFiles()
    {
        WriteFile("z-file.txt", "x");
        WriteFile("a-dir/inner.txt", "x");

        var tree = _builder.Build(_root);
        Assert.Equal("a-dir", tree.Children[0].Name);
        Assert.Equal("z-file.txt", tree.Children[1].Name);
    }

    [Fact]
    public void SortsNamesDeterministically()
    {
        WriteFile("b.txt", "x");
        WriteFile("A.txt", "x");
        WriteFile("c.txt", "x");

        var tree = _builder.Build(_root);
        Assert.Equal(new[] { "A.txt", "b.txt", "c.txt" }, tree.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void CalculatesAggregateDirectorySize()
    {
        WriteFile("dir/a.txt", "12345"); // 5 bytes
        WriteFile("dir/b.txt", "123");   // 3 bytes
        WriteFile("top.txt", "1");       // 1 byte

        var tree = _builder.Build(_root);

        var dir = tree.Children.Single(c => c.Name == "dir");
        Assert.Equal(8, dir.SizeBytes);
        Assert.Equal(9, tree.SizeBytes);
    }

    [Fact]
    public void IncludesFileExtensionAndSize()
    {
        WriteFile("page.tsx", "1234567");

        var tree = _builder.Build(_root);
        var file = tree.Children.Single(c => c.Name == "page.tsx");

        Assert.Equal(".tsx", file.Extension);
        Assert.Equal(7, file.SizeBytes);
    }

    [Fact]
    public void UsesRelativeForwardSlashPaths()
    {
        WriteFile("dir/sub/file.cs", "x");

        var tree = _builder.Build(_root);
        var file = tree.Children[0].Children[0].Children[0];

        Assert.Equal("dir/sub/file.cs", file.RelativePath);
        Assert.DoesNotContain("\\", file.RelativePath);
        Assert.DoesNotContain(_root, file.RelativePath);
    }

    [Fact]
    public void DoesNotExposePhysicalAbsolutePaths()
    {
        WriteFile("file.cs", "x");

        var tree = _builder.Build(_root);
        var text = _builder.BuildText(tree);

        Assert.DoesNotContain(_root, text);
    }
}
