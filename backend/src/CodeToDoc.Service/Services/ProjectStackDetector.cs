using System.Diagnostics;
using System.Text.Json;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Interfaces;

namespace CodeToDoc.Service.Services;

public sealed class ProjectStackDetector : IProjectStackDetector
{
    private readonly List<DetectedStack> _detections = new();
    private readonly List<string> _warnings = new();
    private readonly Stopwatch _stopwatch = new();

    public IReadOnlyList<DetectedStack> Detections => _detections;

    public IReadOnlyList<string> Warnings => _warnings;

    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    public void Reset()
    {
        _detections.Clear();
        _warnings.Clear();
        _stopwatch.Reset();
    }

    public void ScanEntry(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        _stopwatch.Start();
        try
        {
            var fileName = Path.GetFileName(relativePath);
            var extension = Path.GetExtension(fileName);

            switch (extension.ToLowerInvariant())
            {
                case ".csproj": Add(".NET / C#", relativePath); break;
                case ".fsproj": Add(".NET / F#", relativePath); break;
                case ".vbproj": Add(".NET / Visual Basic", relativePath); break;
                case ".sln": Add(".NET solution", relativePath); break;
            }

            switch (fileName.ToLowerInvariant())
            {
                case "package.json": Add("Node.js", relativePath); break;
                case "requirements.txt": Add("Python", relativePath); break;
                case "pyproject.toml": Add("Python", relativePath); break;
                case "pipfile": Add("Python", relativePath); break;
                case "pom.xml": Add("Java", relativePath); break;
                case "build.gradle": Add("Java", relativePath); break;
                case "build.gradle.kts": Add("Java", relativePath); break;
                case "go.mod": Add("Go", relativePath); break;
                case "cargo.toml": Add("Rust", relativePath); break;
                case "composer.json": Add("PHP", relativePath); break;
                case "dockerfile": Add("Docker", relativePath); break;
                case "docker-compose.yml":
                case "docker-compose.yaml":
                case "compose.yml":
                case "compose.yaml": Add("Docker", relativePath); break;
                case "angular.json": Add("Angular", relativePath); break;
            }
        }
        finally
        {
            _stopwatch.Stop();
        }
    }

    public void ScanPackageJson(string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        _stopwatch.Start();
        try
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                DetectDependencies(document.RootElement, "dependencies", relativePath);
                DetectDependencies(document.RootElement, "devDependencies", relativePath);
            }
            catch (JsonException ex)
            {
                _warnings.Add($"Malformed package.json '{relativePath}' could not be parsed: {ex.Message}");
            }
        }
        finally
        {
            _stopwatch.Stop();
        }
    }

    private void DetectDependencies(JsonElement root, string section, string relativePath)
    {
        if (!root.TryGetProperty(section, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var dependency in property.EnumerateObject())
        {
            var evidence = $"{relativePath}: {section}.{dependency.Name}";
            switch (dependency.Name.ToLowerInvariant())
            {
                case "next": Add("Next.js", evidence); break;
                case "react": Add("React", evidence); break;
                case "vue": Add("Vue", evidence); break;
                case "@angular/core": Add("Angular", evidence); break;
            }
        }
    }

    private void Add(string name, string evidence)
    {
        var existing = _detections.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _detections.Add(new DetectedStack { Name = name, Evidence = { evidence } });
        }
        else if (!existing.Evidence.Contains(evidence, StringComparer.OrdinalIgnoreCase))
        {
            existing.Evidence.Add(evidence);
        }
    }
}
