using CodeToDoc.Service.Dtos;

namespace CodeToDoc.Service.Models;

public sealed class PhaseOneWorkspaceResult
{
    public PhaseOneResult Result { get; init; } = null!;

    public JobWorkspace Workspace { get; init; } = null!;
}
