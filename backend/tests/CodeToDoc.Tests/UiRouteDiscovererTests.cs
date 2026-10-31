using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class UiRouteDiscovererTests
{
    private readonly UiRouteDiscoverer _discoverer = new();

    private static FrontendProject Project(string framework, params (string Path, string Content)[] files) =>
        new()
        {
            Id = "frontend",
            Name = "frontend",
            Root = "frontend",
            Framework = framework,
            SourceFiles = files.Select(f => new SourceFile { RelativePath = f.Path, Content = f.Content }).ToList(),
        };

    [Theory]
    [InlineData("app/dashboard/page.tsx", "/dashboard")]
    [InlineData("app/customers/new/page.tsx", "/customers/new")]
    [InlineData("app/customers/[id]/page.tsx", "/customers/[id]")]
    [InlineData("app/(admin)/customers/page.tsx", "/customers")]
    [InlineData("src/app/dashboard/page.tsx", "/dashboard")]
    public void AppRouterPageDetection(string file, string expected)
    {
        var project = Project("next-app-router", (file, "export default function Page() {}"));
        var routes = _discoverer.Discover(project);
        var route = Assert.Single(routes);
        Assert.Equal(expected, route.Route);
        Assert.True(route.IsResolved);
    }

    [Fact]
    public void RouteGroupExcludedFromUrlSegments()
    {
        var project = Project("next-app-router", ("app/(marketing)/about/page.tsx", ""));
        var route = Assert.Single(_discoverer.Discover(project));
        Assert.Equal("/about", route.Route);
    }

    [Fact]
    public void DynamicRouteParametersPreserved()
    {
        var project = Project("next-app-router", ("app/customers/[id]/page.tsx", ""));
        var route = Assert.Single(_discoverer.Discover(project));
        Assert.Equal("/customers/[id]", route.Route);
        Assert.Contains("id", route.RouteParameters);
    }

    [Fact]
    public void LayoutIsNotAStandalonePage()
    {
        var project = Project("next-app-router",
            ("app/dashboard/layout.tsx", ""), ("app/dashboard/page.tsx", ""));
        var routes = _discoverer.Discover(project);
        Assert.Single(routes);
        Assert.Equal("/dashboard", routes[0].Route);
    }

    [Fact]
    public void RouteFileIsNotAUiPage()
    {
        var project = Project("next-app-router",
            ("app/api/users/route.ts", ""), ("app/page.tsx", ""));
        var routes = _discoverer.Discover(project);
        Assert.Single(routes);
        Assert.Equal("/", routes[0].Route);
    }

    [Theory]
    [InlineData("pages/index.tsx", "/")]
    [InlineData("pages/customers/index.tsx", "/customers")]
    [InlineData("pages/customers/new.tsx", "/customers/new")]
    [InlineData("pages/customers/[id].tsx", "/customers/[id]")]
    public void PagesRouterIndexNormalization(string file, string expected)
    {
        var project = Project("next-pages-router", (file, ""));
        var route = Assert.Single(_discoverer.Discover(project));
        Assert.Equal(expected, route.Route);
    }

    [Fact]
    public void PagesApiExcluded()
    {
        var project = Project("next-pages-router",
            ("pages/api/users.ts", ""), ("pages/index.tsx", ""));
        var routes = _discoverer.Discover(project);
        Assert.Single(routes);
        Assert.Equal("/", routes[0].Route);
    }

    [Fact]
    public void ReactRouterExplicitRouteExtraction()
    {
        var project = Project("react-router",
            ("App.tsx", "<Route path=\"/customers\" element={<CustomersPage />} />"));
        var route = Assert.Single(_discoverer.Discover(project));
        Assert.Equal("/customers", route.Route);
    }

    [Fact]
    public void UnrelatedComponentFilesDoNotBecomePages()
    {
        var project = Project("next-app-router",
            ("app/page.tsx", ""), ("components/Header.tsx", "export function Header() {}"));
        var routes = _discoverer.Discover(project);
        Assert.Single(routes);
    }
}
