namespace CodeToDoc.Service.Models;

public sealed class ProjectManifest
{
    public List<ParsedDependency> Dependencies { get; } = new();

    public List<string> FrameworkHints { get; } = new();

    public List<string> Warnings { get; } = new();

    public string? PrimaryLanguage { get; set; }

    public string? TargetFramework { get; set; }
}
