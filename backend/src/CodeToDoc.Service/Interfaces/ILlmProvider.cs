using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface ILlmProvider
{
    Task<LlmProviderResponse> GenerateAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken = default);
}
