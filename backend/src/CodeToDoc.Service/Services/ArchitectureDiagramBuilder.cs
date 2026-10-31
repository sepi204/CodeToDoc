using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Builds an architecture diagram deterministically from validated Phase 2-4
/// skeleton data only. It never consults the LLM. Nodes are repositories,
/// frontend routes, and endpoints; edges are containment and correlation.
/// </summary>
public sealed class ArchitectureDiagramBuilder : IArchitectureDiagramBuilder
{
    public ArchitectureDiagram Build(TechnicalSkeleton technical, UiSkeleton? ui, CorrelationSkeleton? correlation)
    {
        var diagram = new ArchitectureDiagram();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);

        void AddNode(string id, string kind, string label)
        {
            if (nodeIds.Add(id))
            {
                diagram.Nodes.Add(new DiagramNode { Id = id, Kind = kind, Label = Sanitize(label) });
            }
        }

        // Repositories and pages come from the UI skeleton.
        if (ui is not null)
        {
            foreach (var repo in ui.Repositories)
            {
                AddNode($"repo:{repo.Id}", "repository", repo.Name);
            }

            foreach (var page in ui.Pages)
            {
                AddNode($"page:{page.Id}", "page", page.Route ?? page.Title ?? page.Id);
                if (!string.IsNullOrEmpty(page.RepositoryId))
                {
                    AddNode($"repo:{page.RepositoryId}", "repository", page.RepositoryId);
                    diagram.Edges.Add(new DiagramEdge
                    {
                        FromId = $"repo:{page.RepositoryId}",
                        ToId = $"page:{page.Id}",
                        Kind = "contains",
                        Inferred = false,
                    });
                }
            }
        }

        // Endpoints come from the technical skeleton.
        foreach (var endpoint in technical.Endpoints)
        {
            AddNode(EndpointId(endpoint.Method, endpoint.Path), "endpoint", $"{endpoint.Method} {endpoint.Path}");
        }

        // Correlation edges (page -> endpoint). Marked inferred when confidence is below 1.0.
        if (correlation is not null)
        {
            foreach (var c in correlation.Correlations)
            {
                var endpointId = EndpointId(c.Backend.Method, c.Backend.PathTemplate ?? c.Backend.ActionName ?? string.Empty);
                AddNode(endpointId, "endpoint", $"{c.Backend.Method} {c.Backend.PathTemplate}");

                if (!string.IsNullOrEmpty(c.PageId))
                {
                    diagram.Edges.Add(new DiagramEdge
                    {
                        FromId = $"page:{c.PageId}",
                        ToId = endpointId,
                        Kind = "correlates",
                        Inferred = c.Confidence < 1.0,
                    });
                }
            }
        }

        diagram.Available = diagram.Nodes.Count > 0;
        if (!diagram.Available)
        {
            diagram.UnavailableReason = "No validated repositories, routes, or endpoints were detected.";
        }

        return diagram;
    }

    private static string EndpointId(string? method, string path) =>
        $"endpoint:{(method ?? string.Empty).ToUpperInvariant()}:{path ?? string.Empty}";

    /// <summary>Strips control characters so labels are safe to embed in markup.</summary>
    private static string Sanitize(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        return new string(label.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
    }
}
