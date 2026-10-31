using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class OllamaLlmClient : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;

    public OllamaLlmClient(HttpClient httpClient, IOptions<LlmOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<LlmProviderResponse> GenerateAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var payload = new OllamaChatRequest
        {
            Model = request.Model,
            Stream = false,
            Format = "json",
            Messages = new[]
            {
                new OllamaMessage { Role = "system", Content = request.SystemInstruction },
                new OllamaMessage { Role = "user", Content = request.UserPayload },
            },
            Options = new OllamaOptions
            {
                Temperature = _options.Temperature,
                NumCtx = _options.ContextWindow,
            },
        };

        try
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/chat";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, timeoutCts.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failed(LlmErrorCategory.InvalidRequest,
                    $"Model '{request.Model}' not found in local Ollama. Run 'ollama pull {request.Model}'.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failed(LlmErrorCategory.AuthenticationFailure, "Ollama rejected the request (authentication failure).");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Failed(LlmErrorCategory.RateLimited, "Ollama rate-limited the request (HTTP 429).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failed(LlmErrorCategory.TransientProviderFailure,
                    $"Ollama returned HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: timeoutCts.Token);

            return new LlmProviderResponse
            {
                IsSuccess = true,
                Content = body?.Message?.Content ?? string.Empty,
                Usage = new LlmUsage
                {
                    InputTokens = body?.PromptEvalCount ?? 0,
                    OutputTokens = body?.EvalCount ?? 0,
                    TokenCountKind = TokenCountKind.ProviderReported,
                },
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(LlmErrorCategory.Timeout, $"Ollama request timed out after {_options.TimeoutSeconds}s.");
        }
        catch (HttpRequestException)
        {
            return Failed(LlmErrorCategory.TransientProviderFailure,
                $"Ollama service is not reachable at {_options.BaseUrl}. Ensure 'ollama serve' is running.");
        }
    }

    private static LlmProviderResponse Failed(LlmErrorCategory category, string message)
    {
        return new LlmProviderResponse
        {
            IsSuccess = false,
            ErrorCategory = category,
            Content = message,
        };
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("messages")]
        public OllamaMessage[] Messages { get; set; } = Array.Empty<OllamaMessage>();

        [JsonPropertyName("options")]
        public OllamaOptions Options { get; set; } = new();
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
}
