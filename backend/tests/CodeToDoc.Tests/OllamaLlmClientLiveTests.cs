using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeToDoc.Tests;

/// <summary>
/// Live provider test against a locally running Ollama daemon.
/// Skipped by default so that <c>dotnet test</c> never requires an external
/// dependency. To run it, start Ollama (<c>ollama serve</c>), set
/// <c>CODETODOC_LIVE_OLLAMA=1</c>, optionally set
/// <c>CODETODOC_OLLAMA_MODEL</c> to an installed model, and remove the
/// <c>Skip</c> below.
/// </summary>
[Trait("Category", "LocalOllama")]
public class OllamaLlmClientLiveTests
{
    [Fact(Skip = "Live Ollama test. Run 'ollama serve', set CODETODOC_LIVE_OLLAMA=1 and remove this Skip.")]
    public async Task Live_Ollama_ReturnsValidStructuredJson()
    {
        var model = Environment.GetEnvironmentVariable("CODETODOC_OLLAMA_MODEL") ?? "qwen2.5-coder:7b";
        var options = Options.Create(new LlmOptions
        {
            Provider = "Ollama",
            BaseUrl = "http://localhost:11434",
            Model = model,
            TimeoutSeconds = 180,
            ContextWindow = 8192,
            Temperature = 0.1,
        });

        using var http = new HttpClient();
        var client = new OllamaLlmClient(http, options);

        var response = await client.GenerateAsync(new Service.Models.LlmProviderRequest
        {
            Model = model,
            SystemInstruction = "You are a test assistant. Return only the requested JSON and nothing else.",
            UserPayload = "Return the JSON object {\"documentType\":\"technical\",\"moduleId\":\"smoke\",\"title\":\"Smoke\",\"overview\":[],\"technologies\":[],\"architecture\":[],\"authentication\":[],\"prerequisites\":[],\"endpoints\":[],\"limitations\":[]}",
            DocumentType = "technical",
            ModuleId = "smoke",
            TemplateVersion = "live-smoke-v1",
        });

        Assert.True(response.IsSuccess, $"Ollama call failed: {response.Content}");
        Assert.NotEmpty(response.Content);

        var json = System.Text.Json.JsonDocument.Parse(response.Content);
        Assert.Equal("technical", json.RootElement.GetProperty("documentType").GetString());
    }
}
