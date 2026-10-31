namespace CodeToDoc.Service.Models;

public sealed class SynthesisPartition
{
    public string ModuleId { get; init; } = string.Empty;

    public SynthesisPayload Payload { get; init; } = new();
}
