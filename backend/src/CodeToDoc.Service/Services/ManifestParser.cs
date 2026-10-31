using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

public sealed class ManifestParser : IManifestParser
{
    public async Task<ProjectManifest> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var manifest = new ProjectManifest();

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
            var fileName = Path.GetFileName(file);
            var lowerName = fileName.ToLowerInvariant();

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);

                switch (lowerName)
                {
                    case "package.json":
                        ParsePackageJson(relative, content, manifest);
                        break;
                    case "requirements.txt":
                        ParseRequirements(relative, content, manifest);
                        break;
                    case "pyproject.toml":
                        ParsePyproject(relative, content, manifest);
                        break;
                    case "pom.xml":
                        ParsePom(relative, content, manifest);
                        break;
                    case "go.mod":
                        ParseGoMod(relative, content, manifest);
                        break;
                    default:
                        if (lowerName.EndsWith(".csproj") || lowerName.EndsWith(".fsproj") || lowerName.EndsWith(".vbproj"))
                        {
                            ParseCsproj(relative, content, manifest);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                manifest.Warnings.Add($"Could not parse manifest '{relative}': {ex.Message}");
            }
        }

        return manifest;
    }

    private static void ParseCsproj(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("aspnetcore");

        var document = XDocument.Parse(content);
        foreach (var packageReference in document.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
        {
            var name = packageReference.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var version = packageReference.Attribute("Version")?.Value
                ?? packageReference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

            manifest.Dependencies.Add(new ParsedDependency { Name = name, Version = version, Source = relative });
        }

        var targetFramework = document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value;
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            manifest.TargetFramework = targetFramework;
        }
    }

    private static void ParsePackageJson(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("node");

        using var document = JsonDocument.Parse(content);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AddDependencies(document.RootElement, "dependencies", relative, manifest);
        AddDependencies(document.RootElement, "devDependencies", relative, manifest);
    }

    private static void AddDependencies(JsonElement root, string section, string relative, ProjectManifest manifest)
    {
        if (!root.TryGetProperty(section, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var dependency in property.EnumerateObject())
        {
            manifest.Dependencies.Add(new ParsedDependency
            {
                Name = dependency.Name,
                Version = dependency.Value.ValueKind == JsonValueKind.String ? dependency.Value.GetString() : null,
                Source = relative,
            });
        }
    }

    private static void ParseRequirements(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("python");

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('-'))
            {
                continue;
            }

            var parts = Regex.Split(trimmed, @"[<>=!~\[\s]");
            var name = parts[0].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var version = ExtractVersion(trimmed);
            manifest.Dependencies.Add(new ParsedDependency { Name = name, Version = version, Source = relative });
        }
    }

    private static void ParsePyproject(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("python");

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('['))
            {
                continue;
            }

            if (trimmed.StartsWith("dependencies"))
            {
                var match = Regex.Match(trimmed, @"\[(.*)\]");
                if (match.Success)
                {
                    foreach (var dependency in Regex.Split(match.Groups[1].Value, ","))
                    {
                        var name = dependency.Trim().Trim('"', '\'');
                        if (name.Length == 0)
                        {
                            continue;
                        }

                        var parts = Regex.Split(name, @"[<>=!~\[\s]");
                        manifest.Dependencies.Add(new ParsedDependency
                        {
                            Name = parts[0],
                            Version = ExtractVersion(name),
                            Source = relative,
                        });
                    }
                }

                continue;
            }

            var equals = trimmed.Split('=', 2);
            if (equals.Length == 2 && equals[0].Trim().Length > 0)
            {
                manifest.Dependencies.Add(new ParsedDependency
                {
                    Name = equals[0].Trim(),
                    Version = equals[1].Trim().Trim('"', '\''),
                    Source = relative,
                });
            }
        }
    }

    private static void ParsePom(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("java");

        var document = XDocument.Parse(content);
        foreach (var dependency in document.Descendants().Where(e => e.Name.LocalName == "dependency"))
        {
            var groupId = dependency.Elements().FirstOrDefault(e => e.Name.LocalName == "groupId")?.Value;
            var artifactId = dependency.Elements().FirstOrDefault(e => e.Name.LocalName == "artifactId")?.Value;
            var version = dependency.Elements().FirstOrDefault(e => e.Name.LocalName == "version")?.Value;

            if (string.IsNullOrWhiteSpace(artifactId))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(groupId) ? artifactId : $"{groupId}:{artifactId}";
            manifest.Dependencies.Add(new ParsedDependency { Name = name, Version = version, Source = relative });
        }
    }

    private static void ParseGoMod(string relative, string content, ProjectManifest manifest)
    {
        manifest.FrameworkHints.Add("go");

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("require") || trimmed.StartsWith('(') || trimmed.StartsWith(')'))
            {
                continue;
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                manifest.Dependencies.Add(new ParsedDependency { Name = parts[0], Version = parts[1], Source = relative });
            }
        }
    }

    private static string? ExtractVersion(string spec)
    {
        var match = Regex.Match(spec, @"(\d+[\.\-][\w\.\+\-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
