using System.Text.Json;
using System.Text.RegularExpressions;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class UiComponentResolver : IUiComponentResolver
{
    private static readonly string[] ComponentExtensions = { ".tsx", ".jsx", ".ts", ".js" };

    private readonly IUiSourceParser _parser;
    private readonly PhaseThreeProcessingOptions _options;

    public UiComponentResolver(IUiSourceParser parser, IOptions<PhaseThreeProcessingOptions> options)
    {
        _parser = parser;
        _options = options.Value;
    }

    public List<ParsedComponent> ResolveImports(
        SourceFile pageFile,
        ParsedUiSource source,
        FrontendProject project,
        IReadOnlyList<SourceFile> workspaceFiles,
        Action<UiDiagnostic> reportDiagnostic)
    {
        var resolved = new List<ParsedComponent>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesByPath = workspaceFiles.ToDictionary(
            f => f.RelativePath.Replace('\\', '/'),
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var aliases = ParseTsconfigAliases(project);

        foreach (var import in source.Imports)
        {
            ResolveImport(import, pageFile, project, aliases, filesByPath, visited, resolved, reportDiagnostic, depth: 0);
        }

        return resolved;
    }

    private void ResolveImport(
        ParsedImport import,
        SourceFile pageFile,
        FrontendProject project,
        Dictionary<string, string> aliases,
        Dictionary<string, SourceFile> filesByPath,
        HashSet<string> visited,
        List<ParsedComponent> resolved,
        Action<UiDiagnostic> reportDiagnostic,
        int depth)
    {
        if (depth >= _options.MaxComponentDepth)
        {
            reportDiagnostic(new UiDiagnostic
            {
                Code = "ui.component.depth",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Component traversal depth exceeded for import '{import.ModulePath}'.",
                SourceReference = new UiSourceReference { FilePath = pageFile.RelativePath },
            });
            return;
        }

        var file = ResolveFile(import.ModulePath, pageFile.RelativePath, aliases, filesByPath);
        if (file is null)
        {
            reportDiagnostic(new UiDiagnostic
            {
                Code = "ui.component.unresolved",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Could not resolve imported module '{import.ModulePath}'.",
                SourceReference = new UiSourceReference { FilePath = pageFile.RelativePath, StartLine = import.Line },
            });
            return;
        }

        var relative = file.RelativePath.Replace('\\', '/');
        if (!visited.Add(relative))
        {
            return; // cycle detected; terminate safely
        }

        var parsed = _parser.Parse(file, project);
        var name = import.DefaultImport
            ?? import.NamedImports.FirstOrDefault()
            ?? Path.GetFileNameWithoutExtension(relative);

        resolved.Add(new ParsedComponent { Name = name, Source = parsed });

        foreach (var nested in parsed.Imports)
        {
            ResolveImport(nested, file, project, aliases, filesByPath, visited, resolved, reportDiagnostic, depth + 1);
        }
    }

    private static SourceFile? ResolveFile(
        string modulePath,
        string pageRelativePath,
        Dictionary<string, string> aliases,
        Dictionary<string, SourceFile> filesByPath)
    {
        var candidates = BuildCandidatePaths(modulePath, pageRelativePath, aliases);

        foreach (var candidate in candidates)
        {
            if (filesByPath.TryGetValue(candidate, out var file))
            {
                return file;
            }
        }

        return null;
    }

    private static List<string> BuildCandidatePaths(
        string modulePath,
        string pageRelativePath,
        Dictionary<string, string> aliases)
    {
        var candidates = new List<string>();

        if (modulePath.StartsWith('.'))
        {
            // Relative import: resolve against the page directory.
            var pageDirectory = pageRelativePath.Contains('/')
                ? pageRelativePath[..pageRelativePath.LastIndexOf('/')]
                : string.Empty;

            foreach (var extension in ComponentExtensions)
            {
                var relative = Normalize(pageDirectory.Length == 0 ? modulePath : $"{pageDirectory}/{modulePath}");
                candidates.Add(relative + extension);
                candidates.Add($"{relative}/index{extension}");
            }
        }
        else
        {
            // Alias or bare package import.
            var matched = false;
            foreach (var alias in aliases)
            {
                if (modulePath == alias.Key || modulePath.StartsWith(alias.Key + "/", StringComparison.Ordinal))
                {
                    var rest = modulePath == alias.Key ? string.Empty : modulePath[(alias.Key.Length + 1)..];
                    var basePath = alias.Value.TrimEnd('/');
                    foreach (var extension in ComponentExtensions)
                    {
                        var target = rest.Length == 0 ? basePath : $"{basePath}/{rest}";
                        candidates.Add(target + extension);
                        candidates.Add($"{target}/index{extension}");
                    }
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                foreach (var extension in ComponentExtensions)
                {
                    candidates.Add(modulePath + extension);
                }
            }
        }

        return candidates;
    }

    private static Dictionary<string, string> ParseTsconfigAliases(FrontendProject project)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tsconfig = project.SourceFiles.FirstOrDefault(f =>
            Path.GetFileName(f.RelativePath).Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase));

        if (tsconfig is null)
        {
            return aliases;
        }

        try
        {
            using var document = JsonDocument.Parse(tsconfig.Content);
            if (!document.RootElement.TryGetProperty("compilerOptions", out var compilerOptions))
            {
                return aliases;
            }

            var baseUrl = compilerOptions.TryGetProperty("baseUrl", out var baseUrlElement)
                ? baseUrlElement.GetString()?.TrimEnd('/') ?? string.Empty
                : string.Empty;

            if (compilerOptions.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Object)
            {
                foreach (var path in paths.EnumerateObject())
                {
                    var target = path.Value.ValueKind == JsonValueKind.Array && path.Value.GetArrayLength() > 0
                        ? path.Value[0].GetString() ?? string.Empty
                        : string.Empty;

                    target = target.Replace("/*", string.Empty).TrimEnd('/');
                    var key = path.Name.Replace("/*", string.Empty);

                    var full = string.IsNullOrEmpty(baseUrl) ? target : Normalize($"{baseUrl}/{target}");
                    aliases[key] = full;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed tsconfig: no aliases.
        }

        return aliases;
    }

    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('.', '/');
        var parts = new List<string>();
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "." or "")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                continue;
            }

            parts.Add(segment);
        }

        return string.Join("/", parts);
    }
}
