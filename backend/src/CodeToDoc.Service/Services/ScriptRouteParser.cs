using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Deterministic regex-based route parser for TypeScript/JavaScript/Python projects.
/// Used as a fallback when native tree-sitter binaries are not available.
/// </summary>
public static class ScriptRouteParser
{
    private static readonly Regex ExpressRouteRegex = new(
        @"(?:app|router)\.(get|post|put|delete|patch)\(\s*['""]([^'""]+)['""]",
        RegexOptions.IgnoreCase);

    private static readonly Regex DecoratorRouteRegex = new(
        @"@(?:app|router|blueprint)\.(get|post|put|delete|patch)\(\s*['""]([^'""]+)['""]",
        RegexOptions.IgnoreCase);

    public static List<EndpointModel> Parse(IReadOnlyList<SourceFile> sourceFiles)
    {
        var endpoints = new List<EndpointModel>();

        foreach (var file in sourceFiles)
        {
            var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
            if (extension is not (".ts" or ".tsx" or ".js" or ".jsx" or ".py"))
            {
                continue;
            }

            endpoints.AddRange(ParseFile(file));
        }

        return endpoints;
    }

    private static IEnumerable<EndpointModel> ParseFile(SourceFile file)
    {
        foreach (Match match in ExpressRouteRegex.Matches(file.Content))
        {
            yield return new EndpointModel
            {
                Path = match.Groups[2].Value,
                Method = match.Groups[1].Value.ToUpperInvariant(),
            };
        }

        foreach (Match match in DecoratorRouteRegex.Matches(file.Content))
        {
            yield return new EndpointModel
            {
                Path = match.Groups[2].Value,
                Method = match.Groups[1].Value.ToUpperInvariant(),
            };
        }
    }
}
