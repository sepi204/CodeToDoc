using System.Text.Json;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class ProjectRootDiscoverer : IProjectRootDiscoverer
{
    public List<DetectedProjectRoot> Discover(IReadOnlyList<SourceFile> sourceFiles)
    {
        var roots = new List<DetectedProjectRoot>();
        var byDir = new Dictionary<string, List<SourceFile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            var normalized = file.RelativePath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized).ToLowerInvariant();
            if (fileName is "package.json" || fileName.EndsWith(".csproj"))
            {
                var dir = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
                if (!byDir.TryGetValue(dir, out var list))
                {
                    list = new List<SourceFile>();
                    byDir[dir] = list;
                }
                list.Add(file);
            }
        }

        foreach (var (dir, manifests) in byDir.OrderBy(kv => kv.Key.Length))
        {
            foreach (var manifest in manifests)
            {
                var fileName = Path.GetFileName(manifest.RelativePath).ToLowerInvariant();
                if (fileName == "package.json")
                {
                    var classified = ClassifyPackageJson(dir, manifest);
                    if (classified is not null)
                    {
                        roots.Add(classified);
                    }
                }
                else if (fileName.EndsWith(".csproj"))
                {
                    roots.Add(new DetectedProjectRoot
                    {
                        Root = dir,
                        ManifestPath = manifest.RelativePath,
                        Framework = "ASP.NET Core",
                        Language = "C#",
                        Evidence = new List<string> { manifest.RelativePath },
                    });
                }
            }
        }

        // Deduplicate by root: keep most specific framework (NestJS/Astro over generic Node.js).
        var deduped = new Dictionary<string, DetectedProjectRoot>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.OrderBy(r => Specificity(r.Framework)))
        {
            deduped[root.Root] = root;
        }

        return deduped.Values.OrderBy(r => r.Root.Length).ToList();
    }

    private static int Specificity(string framework) => framework switch
    {
        "NestJS" => 0,
        "Astro" => 0,
        "Next.js" => 1,
        "Express" or "Fastify" => 1,
        "React" or "Vue" or "Angular" => 2,
        "ASP.NET Core" => 0,
        _ => 10,
    };

    private static DetectedProjectRoot? ClassifyPackageJson(string dir, SourceFile manifest)
    {
        try
        {
            using var doc = JsonDocument.Parse(manifest.Content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (doc.RootElement.TryGetProperty(section, out var prop) && prop.ValueKind == JsonValueKind.Object)
                {
                    foreach (var d in prop.EnumerateObject())
                    {
                        deps.Add(d.Name);
                    }
                }
            }

            var lower = deps.Select(d => d.ToLowerInvariant()).ToHashSet();
            string framework = "Node.js";
            string language = "TypeScript";

            if (lower.Contains("@nestjs/core") || lower.Contains("@nestjs/common") || manifest.Content.Contains("nest-cli"))
            {
                framework = "NestJS";
            }
            else if (lower.Contains("astro"))
            {
                framework = "Astro";
            }
            else if (lower.Contains("next"))
            {
                framework = "Next.js";
            }
            else if (lower.Contains("@angular/core"))
            {
                framework = "Angular";
                language = "TypeScript";
            }
            else if (lower.Contains("vue") || lower.Contains("nuxt"))
            {
                framework = lower.Contains("nuxt") ? "Nuxt" : "Vue";
            }
            else if (lower.Contains("react"))
            {
                framework = "React";
            }
            else if (lower.Contains("express"))
            {
                framework = "Express";
            }
            else if (lower.Contains("fastify") || lower.Contains("@fastify/static"))
            {
                framework = "Fastify";
            }

            return new DetectedProjectRoot
            {
                Root = dir,
                ManifestPath = manifest.RelativePath,
                Framework = framework,
                Language = language,
                Evidence = new List<string> { manifest.RelativePath },
            };
        }
        catch (JsonException)
        {
            return new DetectedProjectRoot
            {
                Root = dir,
                ManifestPath = manifest.RelativePath,
                Framework = "Node.js",
                Language = "JavaScript",
                Evidence = new List<string> { manifest.RelativePath },
            };
        }
    }
}
