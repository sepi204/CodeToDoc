namespace CodeToDoc.Service.Models;

public sealed class JobWorkspace
{
    public string JobId { get; init; } = string.Empty;

    public string JobRootPath { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;
}
