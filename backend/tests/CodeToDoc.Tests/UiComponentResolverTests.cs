using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Tests;

public class UiComponentResolverTests
{
    private readonly UiComponentResolver _resolver =
        new(new UiSourceParser(), Options.Create(new PhaseThreeProcessingOptions()));

    private static FrontendProject Project(params SourceFile[] files) =>
        new() { Id = "frontend", Framework = "next-app-router", SourceFiles = files };

    private static SourceFile File(string path, string content) =>
        new() { RelativePath = path, Content = content };

    [Fact]
    public void ResolvesRelativeImport()
    {
        var page = File("src/app/customers/new/page.tsx", "import CustomerForm from './CustomerForm';");
        var form = File("src/app/customers/new/CustomerForm.tsx", "<form><input name=\"lastName\" /></form>");
        var source = new ParsedUiSource();
        source.Imports.Add(new ParsedImport { ModulePath = "./CustomerForm", DefaultImport = "CustomerForm" });

        var resolved = _resolver.ResolveImports(page, source, Project(page, form), Project(page, form).SourceFiles, _ => { });

        var component = Assert.Single(resolved);
        Assert.Equal("CustomerForm", component.Name);
        Assert.Single(component.Source.Forms);
    }

    [Fact]
    public void CyclicImportsTerminateSafely()
    {
        var page = File("page.tsx", "import A from './a';");
        var a = File("a.tsx", "import B from './b';");
        var b = File("b.tsx", "import A from './a';");
        var source = new ParsedUiSource();
        source.Imports.Add(new ParsedImport { ModulePath = "./a", DefaultImport = "A" });

        var project = Project(page, a, b);
        var resolved = _resolver.ResolveImports(page, source, project, project.SourceFiles, _ => { });

        Assert.True(resolved.Count <= 2);
    }

    [Fact]
    public void UnresolvedImportReportsDiagnostic()
    {
        var page = File("page.tsx", "import X from './missing';");
        var source = new ParsedUiSource();
        source.Imports.Add(new ParsedImport { ModulePath = "./missing", DefaultImport = "X" });
        var project = Project(page);

        var diagnostics = new List<UiDiagnostic>();
        var resolved = _resolver.ResolveImports(page, source, project, project.SourceFiles, d => diagnostics.Add(d));

        Assert.Empty(resolved);
        Assert.NotEmpty(diagnostics);
    }
}
