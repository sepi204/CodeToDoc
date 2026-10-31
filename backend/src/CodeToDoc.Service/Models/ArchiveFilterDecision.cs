using CodeToDoc.Service.Enums;

namespace CodeToDoc.Service.Models;

public sealed class ArchiveFilterDecision
{
    public static ArchiveFilterDecision Accept { get; } = new() { ShouldSkip = false };

    public bool ShouldSkip { get; private init; }

    public SkipReason? Reason { get; private init; }

    public static ArchiveFilterDecision Skip(SkipReason reason) =>
        new() { ShouldSkip = true, Reason = reason };
}
