using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IPromptBuilder
{
    PromptResult BuildTechnical(SynthesisPayload payload);

    PromptResult BuildUserGuide(SynthesisPayload payload);
}
