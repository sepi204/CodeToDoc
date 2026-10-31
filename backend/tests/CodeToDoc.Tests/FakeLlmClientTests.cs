using System.Text.Json;
using CodeToDoc.Service.Dtos;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class FakeLlmClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly FakeLlmClient _fake = new();
    private readonly PromptBuilder _promptBuilder = new();

    [Fact]
    public async Task ReturnsValidTechnicalResponse()
    {
        var payload = new SynthesisPayload
        {
            DocumentType = "technical",
            ModuleId = "invoices",
            Endpoints = { new PayloadEndpoint { EndpointId = "ep_0", Method = "POST", Path = "/api/invoices" } },
        };
        var prompt = _promptBuilder.BuildTechnical(payload);

        var response = await _fake.GenerateAsync(new LlmProviderRequest
        {
            SystemInstruction = prompt.SystemInstruction,
            UserPayload = prompt.UserPayload,
            DocumentType = "technical",
            ModuleId = "invoices",
            TemplateVersion = prompt.TemplateVersion,
        });

        Assert.True(response.IsSuccess);
        var technical = JsonSerializer.Deserialize<TechnicalModuleResponse>(response.Content, JsonOptions)!;
        Assert.Equal("technical", technical.DocumentType);
        Assert.Equal("ep_0", Assert.Single(technical.Endpoints).EndpointId);
    }

    [Fact]
    public async Task ReturnsValidUserGuideResponse()
    {
        var payload = new SynthesisPayload
        {
            DocumentType = "userGuide",
            ModuleId = "invoices",
            Pages = { new PayloadPage { PageId = "page_1", Route = "/invoices/new", Title = "صدور فاکتور", Forms = { new PayloadForm { Fields = { new PayloadField { Name = "customerId", Label = "شناسه مشتری" } } } } } },
        };
        var prompt = _promptBuilder.BuildUserGuide(payload);

        var response = await _fake.GenerateAsync(new LlmProviderRequest
        {
            SystemInstruction = prompt.SystemInstruction,
            UserPayload = prompt.UserPayload,
            DocumentType = "userGuide",
            ModuleId = "invoices",
            TemplateVersion = prompt.TemplateVersion,
        });

        Assert.True(response.IsSuccess);
        var guide = JsonSerializer.Deserialize<UserGuideModuleResponse>(response.Content, JsonOptions)!;
        Assert.Equal("userGuide", guide.DocumentType);
        var page = Assert.Single(guide.Pages);
        Assert.Equal("page_1", page.PageId);
        Assert.Contains("شناسه مشتری", page.Forms[0].Steps[0].Text);
    }

    [Fact]
    public async Task ScriptedFailureIsReturned()
    {
        _fake.ScriptNext(() => new LlmProviderResponse
        {
            IsSuccess = false,
            ErrorCategory = LlmErrorCategory.RateLimited,
            Content = "rate limited",
        });

        var response = await _fake.GenerateAsync(new LlmProviderRequest { DocumentType = "technical" });

        Assert.False(response.IsSuccess);
        Assert.Equal(LlmErrorCategory.RateLimited, response.ErrorCategory);
    }

    [Fact]
    public async Task RecordsInvocationCountAndUsage()
    {
        await _fake.GenerateAsync(new LlmProviderRequest { DocumentType = "technical" });
        await _fake.GenerateAsync(new LlmProviderRequest { DocumentType = "technical" });

        Assert.Equal(2, _fake.InvocationCount);

        var response = await _fake.GenerateAsync(new LlmProviderRequest { DocumentType = "technical" });
        Assert.Equal(TokenCountKind.ProviderReported, response.Usage.TokenCountKind);
        Assert.True(response.Usage.InputTokens > 0);
    }
}
