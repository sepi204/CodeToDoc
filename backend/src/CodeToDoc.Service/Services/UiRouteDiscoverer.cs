using System.Text.RegularExpressions;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class UiRouteDiscoverer : IUiRouteDiscoverer
{
    private static readonly Regex AppRouterFileRegex = new(
        @"(^|/)(?:src/)?app/",
        RegexOptions.IgnoreCase);

    private static readonly Regex PageFileRegex = new(
        @"^page\.(tsx|jsx|ts|js)$",
        RegexOptions.IgnoreCase);

    private static readonly Regex PagesRouterFileRegex = new(
        @"(^|/)(?:src/)?pages/",
        RegexOptions.IgnoreCase);

    private static readonly Regex RazorPageRegex = new(
        @"@page\s+""([^""]+)""",
        RegexOptions.IgnoreCase);

    private static readonly Regex ReactRouteRegex = new(
        @"<Route\b[^>]*\bpath\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase);

    private static readonly Regex ReactRouterObjectRegex = new(
        @"path\s*:\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase);

    public List<DiscoveredRoute> Discover(FrontendProject project)
    {
        var routes = new List<DiscoveredRoute>();

        foreach (var file in project.SourceFiles)
        {
            var normalized = file.RelativePath.Replace('\\', '/');

            switch (project.Framework)
            {
                case "next-app-router":
                    var appRoute = DiscoverAppRouter(normalized, project.Id);
                    if (appRoute is not null) routes.Add(appRoute);
                    break;

                case "next-pages-router":
                    var pagesRoute = DiscoverPagesRouter(normalized, project.Id);
                    if (pagesRoute is not null) routes.Add(pagesRoute);
                    break;

                case "react-router":
                    routes.AddRange(DiscoverReactRouter(file, project.Id));
                    break;

                case "razor":
                    var razorRoute = DiscoverRazor(file, project.Id);
                    if (razorRoute is not null) routes.Add(razorRoute);
                    break;

                case "astro":
                    var astroRoute = DiscoverAstro(normalized, project.Id);
                    if (astroRoute is not null) routes.Add(astroRoute);
                    break;

                case "html":
                    routes.Add(new DiscoveredRoute
                    {
                        RepositoryId = project.Id,
                        SourceFile = normalized,
                        Route = null,
                        RouteExpression = normalized,
                        IsResolved = false,
                    });
                    break;
            }
        }

        return routes;
    }

    private static DiscoveredRoute? DiscoverAppRouter(string relativePath, string repositoryId)
    {
        if (!AppRouterFileRegex.IsMatch(relativePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(relativePath);
        if (!PageFileRegex.IsMatch(fileName))
        {
            return null;
        }

        var match = Regex.Match(relativePath, @"(?:^|/)(?:src/)?app/(.*)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var segments = match.Groups[1].Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Drop the page file segment.
        segments = segments[..^1];

        var urlSegments = new List<string>();
        var routeParameters = new List<string>();
        var unsupported = false;

        foreach (var segment in segments)
        {
            if (segment.StartsWith('(') && segment.EndsWith(')'))
            {
                continue; // route group
            }

            if (segment.StartsWith('_'))
            {
                return null; // private folder
            }

            if (segment.StartsWith('@') || (segment.Length > 1 && segment[0] == '('))
            {
                unsupported = true; // parallel / intercepting route
                break;
            }

            if (segment.StartsWith("[[...") && segment.EndsWith("]]"))
            {
                urlSegments.Add(segment);
                routeParameters.Add(segment.Trim('[', ']'));
                continue;
            }

            if (segment.StartsWith("[...") && segment.EndsWith(']'))
            {
                urlSegments.Add(segment);
                routeParameters.Add(segment.Trim('[', ']'));
                continue;
            }

            if (segment.StartsWith('[') && segment.EndsWith(']'))
            {
                urlSegments.Add(segment);
                routeParameters.Add(segment.Trim('[', ']'));
                continue;
            }

            urlSegments.Add(segment);
        }

        if (unsupported)
        {
            return new DiscoveredRoute
            {
                RepositoryId = repositoryId,
                SourceFile = relativePath,
                Route = null,
                RouteExpression = relativePath,
                IsResolved = false,
            };
        }

        var route = urlSegments.Count == 0 ? "/" : "/" + string.Join("/", urlSegments);
        return new DiscoveredRoute
        {
            RepositoryId = repositoryId,
            SourceFile = relativePath,
            Route = route,
            RouteParameters = routeParameters,
            IsResolved = true,
        };
    }

    private static DiscoveredRoute? DiscoverPagesRouter(string relativePath, string repositoryId)
    {
        if (!PagesRouterFileRegex.IsMatch(relativePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(relativePath);
        if (fileName.StartsWith('_'))
        {
            return null; // _app, _document, etc.
        }

        if (!Regex.IsMatch(relativePath, @"\.(tsx|jsx|ts|js)$", RegexOptions.IgnoreCase))
        {
            return null;
        }

        var match = Regex.Match(relativePath, @"(?:^|/)(?:src/)?pages/(.*)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var path = match.Groups[1].Value;
        if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            return null; // API route, not UI page
        }

        var withoutExtension = Regex.Replace(path, @"\.(tsx|jsx|ts|js)$", "", RegexOptions.IgnoreCase);
        var segments = withoutExtension.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        var routeParameters = new List<string>();
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].StartsWith('[') && segments[i].EndsWith(']'))
            {
                routeParameters.Add(segments[i].Trim('[', ']'));
            }
        }

        if (segments.Count > 0 && segments[^1].Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        var route = segments.Count == 0 ? "/" : "/" + string.Join("/", segments);
        return new DiscoveredRoute
        {
            RepositoryId = repositoryId,
            SourceFile = relativePath,
            Route = route,
            RouteParameters = routeParameters,
            IsResolved = true,
        };
    }

    private static List<DiscoveredRoute> DiscoverReactRouter(SourceFile file, string repositoryId)
    {
        var routes = new List<DiscoveredRoute>();

        foreach (Match match in ReactRouteRegex.Matches(file.Content))
        {
            routes.Add(new DiscoveredRoute
            {
                RepositoryId = repositoryId,
                SourceFile = file.RelativePath,
                Route = match.Groups[1].Value,
                IsResolved = true,
            });
        }

        if (routes.Count == 0)
        {
            foreach (Match match in ReactRouterObjectRegex.Matches(file.Content))
            {
                routes.Add(new DiscoveredRoute
                {
                    RepositoryId = repositoryId,
                    SourceFile = file.RelativePath,
                    Route = match.Groups[1].Value,
                    IsResolved = true,
                });
            }
        }

        return routes;
    }

    private static DiscoveredRoute? DiscoverRazor(SourceFile file, string repositoryId)
    {
        var match = RazorPageRegex.Match(file.Content);
        if (!match.Success)
        {
            return null;
        }

        return new DiscoveredRoute
        {
            RepositoryId = repositoryId,
            SourceFile = file.RelativePath,
            Route = match.Groups[1].Value,
            IsResolved = true,
        };
    }

    private static DiscoveredRoute? DiscoverAstro(string relativePath, string repositoryId)
    {
        var lower = relativePath.ToLowerInvariant();
        if (!lower.EndsWith(".astro"))
        {
            return null;
        }

        // Astro file-based routing: src/pages/**/*.astro -> /route.
        // Skip API routes (handled by AstroRouteParser in Phase 2).
        var pagesIdx = lower.IndexOf("/pages/", StringComparison.Ordinal);
        if (pagesIdx < 0)
        {
            pagesIdx = lower.IndexOf("pages/", StringComparison.Ordinal);
            if (pagesIdx < 0) return null;
        }

        var afterPages = relativePath[(pagesIdx + "/pages".Length)..].TrimStart('/');
        if (afterPages.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var withoutExt = Regex.Replace(afterPages, @"\.astro$", "", RegexOptions.IgnoreCase);
        var segments = withoutExt.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var routeParams = new List<string>();
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].StartsWith('[') && segments[i].EndsWith(']'))
            {
                routeParams.Add(segments[i].Trim('[', ']'));
            }
        }

        if (segments.Count > 0 && segments[^1].Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        var route = segments.Count == 0 ? "/" : "/" + string.Join("/", segments);
        return new DiscoveredRoute
        {
            RepositoryId = repositoryId,
            SourceFile = relativePath,
            Route = route,
            RouteParameters = routeParams,
            IsResolved = true,
        };
    }
}
