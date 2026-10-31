namespace CodeToDoc.Service.Models;

public sealed class PromptResult
{
    public string SystemInstruction { get; init; } = string.Empty;

    public string UserPayload { get; init; } = string.Empty;

    public string TemplateVersion { get; init; } = string.Empty;
}
