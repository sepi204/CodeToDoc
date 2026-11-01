using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public static class AstroRouteParser
{
    private static readonly Regex ExportFuncRegex = new(
        @"export\s+(?:async\s+)?function\s+(?<verb>GET|POST|PUT|DELETE|PATCH|OPTIONS|HEAD)\b",
        RegexOptions.IgnoreCase);

    public static List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles)
    {
        var endpoints = new List<EndpointModel>();

        foreach (var file in sourceFiles)
        {
            var normalized = file.RelativePath.Replace('\\', '/');
            var lower = normalized.ToLowerInvariant();

            // Astro API routes: src/pages/api/**/*.ts|js
            var apiIdx = lower.IndexOf("/pages/api/", StringComparison.Ordinal);
            if (apiIdx < 0)
            {
                var altIdx = lower.IndexOf("pages/api/", StringComparison.Ordinal);
                if (altIdx < 0) continue;
                apiIdx = altIdx + "pages".Length;
            }

            var afterPages = normalized[(apiIdx + "/pages".Length)..];
            // afterPages like /api/health.ts
            var withoutExt = Regex.Replace(afterPages, @"\.(ts|js|mts|mjs)$", "", RegexOptions.IgnoreCase);
            var route = withoutExt;
            if (route.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
            {
                route = route[..^"/index".Length];
                if (string.IsNullOrEmpty(route)) route = "/";
            }

            var verbs = ExportFuncRegex.Matches(file.Content)
                .Select(m => m.Groups["verb"].Value.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (verbs.Count == 0)
            {
                verbs.Add(InferVerb(normalized, file.Content));
            }

            foreach (var verb in verbs)
            {
                endpoints.Add(new EndpointModel
                {
                    Path = route,
                    Method = verb,
                    ControllerName = "Astro",
                    ActionName = Path.GetFileNameWithoutExtension(normalized),
                    SourceProject = ProjectRootOf(normalized),
                });
            }
        }

        return endpoints;
    }

    private static string InferVerb(string path, string content)
    {
        if (content.Contains("form", StringComparison.OrdinalIgnoreCase) &&
            content.Contains("POST", StringComparison.OrdinalIgnoreCase))
        {
            return "POST";
        }
        return "GET";
    }

    private static string? ProjectRootOf(string relativePath)
    {
        var idx = relativePath.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return relativePath[..idx].Trim('/');
        }
        var pages = relativePath.IndexOf("/pages/", StringComparison.OrdinalIgnoreCase);
        if (pages >= 0)
        {
            return relativePath[..pages].Trim('/');
        }
        return null;
    }
}
