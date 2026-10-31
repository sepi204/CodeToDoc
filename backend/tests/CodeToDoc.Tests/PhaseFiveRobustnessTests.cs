using System.Text.Json;
using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Tests;

/// <summary>
/// Phase 5 must never crash on malformed LLM responses (e.g. models that omit
/// or null out collections). Regression coverage for the PnaraV2-style crash.
/// </summary>
public class PhaseFiveRobustnessTests : IDisposable
{
    private readonly string _root = TestHelpers.CreateTempRoot();

    public void Dispose() => TestHelpers.DeleteDirectory(_root);

    private string CreateWorkspace()
    {
        var jobRoot = Path.Combine(_root, "job1");
        Directory.CreateDirectory(Path.Combine(jobRoot, "source"));

        File.WriteAllText(Path.Combine(jobRoot, "technical_skeleton.json"), """
            {
              "jobId": "job1",
              "generatedAtUtc": "2026-01-01T00:00:00Z",
              "summary": { "primaryLanguage": "C#" },
              "dependencies": [],
              "endpoints": [],
              "warnings": []
            }
            """);

        File.WriteAllText(Path.Combine(jobRoot, "ui_skeleton.json"), """
            {
              "schemaVersion": "1.0",
              "jobId": "job1",
              "generatedAtUtc": "2026-01-01T00:00:00Z",
              "status": "complete",
              "repositories": [],
              "pages": [
                {
                  "id": "page_1",
                  "repositoryId": "repo1",
                  "route": "/account",
                  "title": null,
                  "sourceFile": "account.tsx",
                  "routeParameters": [],
                  "forms": [],
                  "actions": [
                    { "id": "action_1", "label": "Click me", "elementType": "button", "kind": "navigate", "formId": null, "navigationTarget": "/home", "apiReferences": [], "evidenceIds": [] }
                  ],
                  "tables": [],
                  "scenarios": [],
                  "children": [],
                  "evidenceIds": []
                }
              ],
              "navigation": [],
              "sharedComponents": [],
              "evidence": [],
              "diagnostics": []
            }
            """);

        return jobRoot;
    }

    [Fact]
    public async Task MalformedLlmResponse_DoesNotCrash()
    {
        var fake = new FakeLlmClient();
        fake.ScriptNext(() => new LlmProviderResponse
        {
            IsSuccess = true,
            // A structurally-plausible but null-heavy response a small model
            // might emit: documentType matches, but nested collections are null.
            Content = """
                {
                  "schemaVersion": "1.0",
                  "documentType": "userGuide",
                  "moduleId": "general",
                  "title": null,
                  "overview": null,
                  "pages": [
                    {
                      "pageId": "page_1",
                      "displayTitle": null,
                      "introduction": null,
                      "forms": null,
                      "actions": [ { "actionId": "action_1", "label": null, "outcome": null, "certainty": null } ],
                      "tables": null
                    }
                  ],
                  "limitations": null
                }
                """,
            Usage = new LlmUsage { InputTokens = 1, OutputTokens = 1, TokenCountKind = TokenCountKind.ProviderReported },
        });

        var processor = new PhaseFiveProcessor(
            fake,
            new PromptBuilder(),
            Options.Create(new LlmOptions
            {
                Provider = "Fake",
                Model = "fake",
                TechnicalDocumentLanguage = "en",
                UserGuideLanguage = "fa",
            }),
            NullLogger<PhaseFiveProcessor>.Instance);

        var jobRoot = CreateWorkspace();
        var workspace = new JobWorkspace
        {
            JobId = "job1",
            JobRootPath = jobRoot,
            SourcePath = Path.Combine(jobRoot, "source"),
        };

        // Must not throw; the malformed response is normalized and rendered.
        var manifest = await processor.ProcessAsync(workspace);

        Assert.NotNull(manifest);
        var userGuide = Assert.Single(manifest.Documents, d => d.DocumentType == "userGuide");
        // The response parsed (documentType matched) so the guide is rendered.
        Assert.NotEqual(SynthesisStatus.Failed, userGuide.Status);
        Assert.True(File.Exists(Path.Combine(jobRoot, "user_guide.md")));
    }

    [Fact]
    public async Task MalformedJsonResponse_IsRejectedAsPartial_NotCrash()
    {
        var fake = new FakeLlmClient();
        fake.ScriptNext(() => new LlmProviderResponse
        {
            IsSuccess = true,
            Content = "this is not json at all",
            Usage = new LlmUsage { InputTokens = 1, OutputTokens = 1, TokenCountKind = TokenCountKind.ProviderReported },
        });

        var processor = new PhaseFiveProcessor(
            fake,
            new PromptBuilder(),
            Options.Create(new LlmOptions
            {
                Provider = "Fake",
                Model = "fake",
                TechnicalDocumentLanguage = "en",
                UserGuideLanguage = "fa",
            }),
            NullLogger<PhaseFiveProcessor>.Instance);

        var jobRoot = CreateWorkspace();
        var workspace = new JobWorkspace
        {
            JobId = "job1",
            JobRootPath = jobRoot,
            SourcePath = Path.Combine(jobRoot, "source"),
        };

        var manifest = await processor.ProcessAsync(workspace);

        Assert.NotNull(manifest);
        var userGuide = Assert.Single(manifest.Documents, d => d.DocumentType == "userGuide");
        Assert.Equal(SynthesisStatus.Partial, userGuide.Status);
    }
}
