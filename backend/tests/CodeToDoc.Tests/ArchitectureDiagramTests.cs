using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class ArchitectureDiagramTests
{
    private readonly ArchitectureDiagramBuilder _builder = new();

    [Fact]
    public void Empty_IsUnavailable()
    {
        var diagram = _builder.Build(new TechnicalSkeleton(), null, null);

        Assert.False(diagram.Available);
        Assert.NotNull(diagram.UnavailableReason);
    }

    [Fact]
    public void Valid_ProducesNodesAndEdges()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = { new EndpointModel { Method = "POST", Path = "/api/invoices" } },
        };
        var ui = new UiSkeleton
        {
            Repositories = { new UiRepository { Id = "frontend", Name = "frontend" } },
            Pages = { new UiPage { Id = "page_1", RepositoryId = "frontend", Route = "/invoices/new" } },
        };
        var correlation = new CorrelationSkeleton
        {
            Correlations =
            {
                new Correlation
                {
                    PageId = "page_1",
                    Confidence = 1,
                    Backend = new CorrelationBackend { Method = "POST", PathTemplate = "/api/invoices" },
                },
            },
        };

        var diagram = _builder.Build(technical, ui, correlation);

        Assert.True(diagram.Available);
        Assert.Contains(diagram.Nodes, n => n.Kind == "endpoint" && n.Label == "POST /api/invoices");
        Assert.Contains(diagram.Nodes, n => n.Kind == "page" && n.Label == "/invoices/new");
        Assert.Contains(diagram.Edges, e => e.Kind == "correlates" && !e.Inferred);
    }

    [Fact]
    public void LowConfidenceCorrelation_IsMarkedInferred()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = { new EndpointModel { Method = "GET", Path = "/api/users" } },
        };
        var correlation = new CorrelationSkeleton
        {
            Correlations =
            {
                new Correlation
                {
                    PageId = "page_1",
                    Confidence = 0.5,
                    Backend = new CorrelationBackend { Method = "GET", PathTemplate = "/api/users" },
                },
            },
        };

        var diagram = _builder.Build(technical, null, correlation);

        var edge = Assert.Single(diagram.Edges);
        Assert.True(edge.Inferred);
    }

    [Fact]
    public void MaliciousLabel_IsSanitized()
    {
        var technical = new TechnicalSkeleton
        {
            Endpoints = { new EndpointModel { Method = "POST", Path = "evil\x00path" } },
        };

        var diagram = _builder.Build(technical, null, null);

        var endpoint = Assert.Single(diagram.Nodes);
        Assert.DoesNotContain('\0', endpoint.Label);
        Assert.DoesNotContain("<script>", endpoint.Label);
    }
}
