using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class ProjectRootDiscovererTests
{
    private readonly ProjectRootDiscoverer _sut = new();

    private static SourceFile PackageJson(string relativePath, string content) =>
        new() { RelativePath = relativePath, Content = content };

    [Fact]
    public void Discovers_Nested_NestJs_Api_Root()
    {
        var files = new List<SourceFile>
        {
            PackageJson("MovieToon/package.json", """{"dependencies":{"astro":"4.0.0","react":"18.2.0"}}"""),
            PackageJson("MovieToon/api/package.json", """{"dependencies":{"@nestjs/common":"10.0.0","@nestjs/core":"10.0.0","typeorm":"0.3.0","pg":"8.11.0"}}"""),
        };

        var roots = _sut.Discover(files);

        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, r => r.Framework == "Astro");
        var api = Assert.Single(roots, r => r.Root.EndsWith("api", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("NestJS", api.Framework);
    }

    [Fact]
    public void Deduplicates_Same_Root_Prefers_Specific_Framework()
    {
        var files = new List<SourceFile>
        {
            PackageJson("app/package.json", """{"dependencies":{"express":"4.18.0"}}"""),
        };

        var roots = _sut.Discover(files);

        var single = Assert.Single(roots);
        Assert.Equal("Express", single.Framework);
    }
}
