using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

public sealed class LlmProviderResponse
{
    public bool IsSuccess { get; init; }

    public string Content { get; init; } = string.Empty;

    public LlmUsage Usage { get; init; } = new();

    public string? ProviderRequestId { get; init; }

    public LlmErrorCategory? ErrorCategory { get; init; }
}

public sealed class LlmUsage
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public TokenCountKind TokenCountKind { get; init; }
}
