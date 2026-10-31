using System.Text.RegularExpressions;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class FrontendProjectDetector : IFrontendProjectDetector
{
    private static readonly string[] FrontendExtensions = { ".tsx", ".jsx", ".html", ".razor", ".vue", ".ts", ".js" };

    public List<FrontendProject> Detect(string sourcePath, IReadOnlyList<SourceFile> sourceFiles)
    {
        var projects = new List<FrontendProject>();

        foreach (var group in sourceFiles.GroupBy(f => GetRepositoryId(f.RelativePath)))
        {
            var files = group.ToList();
            if (!IsFrontend(files))
            {
                continue;
            }

            projects.Add(new FrontendProject
            {
                Id = group.Key,
                Name = group.Key,
                Root = group.Key,
                Framework = DetectFramework(files),
                SourceFiles = files,
            });
        }

        return projects;
    }

    private static string GetRepositoryId(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var index = normalized.IndexOf('/');
        return index < 0 ? "root" : normalized[..index];
    }

    private static bool IsFrontend(IReadOnlyList<SourceFile> files)
    {
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
            if (FrontendExtensions.Contains(extension))
            {
                return true;
            }

            if (Path.GetFileName(file.RelativePath).Equals("package.json", StringComparison.OrdinalIgnoreCase))
            {
                var lower = file.Content.ToLowerInvariant();
                if (lower.Contains("\"next\"") || lower.Contains("\"react\"") || lower.Contains("\"vue\""))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string DetectFramework(IReadOnlyList<SourceFile> files)
    {
        var packageJson = files.FirstOrDefault(f =>
            Path.GetFileName(f.RelativePath).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        var packageLower = packageJson?.Content.ToLowerInvariant() ?? string.Empty;

        var hasAppRouter = files.Any(f => Regex.IsMatch(
            f.RelativePath.Replace('\\', '/'),
            @"(^|/)(app|src/app)/(?:.*/)?page\.(tsx|jsx|ts|js)$", RegexOptions.IgnoreCase));

        var hasPagesRouter = files.Any(f => Regex.IsMatch(
            f.RelativePath.Replace('\\', '/'),
            @"(^|/)(pages|src/pages)/.*\.(tsx|jsx|ts|js)$", RegexOptions.IgnoreCase));

        if (packageLower.Contains("\"next\""))
        {
            return hasAppRouter ? "next-app-router" : "next-pages-router";
        }

        if (hasAppRouter) return "next-app-router";
        if (hasPagesRouter) return "next-pages-router";

        if (files.Any(f => f.RelativePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))) return "razor";
        if (files.Any(f => f.RelativePath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))) return "vue";
        if (files.Any(f => f.RelativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))) return "html";
        if (packageLower.Contains("\"react\"")) return "react-router";

        return "unknown";
    }
}
