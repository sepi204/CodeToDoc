using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IArchitectureInferenceEngine
{
    ArchitectureInferenceResult Infer(ProjectManifest manifest);
}
