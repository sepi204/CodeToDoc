using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class AstroRouteParserTests
{
    [Fact]
    public void Parses_Astro_Api_Routes()
    {
        var files = new List<SourceFile>
        {
            new() { RelativePath = "MovieToon/src/pages/api/health.ts", Content = "export async function GET() { return new Response('ok'); }" },
            new() { RelativePath = "MovieToon/src/pages/api/movies/[id].ts", Content = "export async function GET() {} export async function PUT() {}" },
            new() { RelativePath = "MovieToon/src/pages/index.astro", Content = "<html>home</html>" },
        };

        var endpoints = AstroRouteParser.Parse(files);

        Assert.Contains(endpoints, e => e.Path == "/api/health" && e.Method == "GET");
        Assert.Contains(endpoints, e => e.Path == "/api/movies/[id]" && e.Method == "PUT");
    }
}
