namespace CodeToDoc.Service.Models;

public sealed class LlmProviderRequest
{
    public string Model { get; init; } = string.Empty;

    public string SystemInstruction { get; init; } = string.Empty;

    public string UserPayload { get; init; } = string.Empty;

    public string DocumentType { get; init; } = string.Empty;

    public string ModuleId { get; init; } = string.Empty;

    public string TemplateVersion { get; init; } = string.Empty;
}
