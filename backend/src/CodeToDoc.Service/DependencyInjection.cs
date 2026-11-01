using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Options;
using CodeToDoc.Service.Services;
using CodeToDoc.Service.Services.Documents;
using CodeToDoc.Service.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IProjectService, ProjectService>();

        services.AddOptions<PhaseOneProcessingOptions>()
            .Bind(configuration.GetSection(PhaseOneProcessingOptions.SectionName));

        services.AddSingleton<IValidateOptions<PhaseOneProcessingOptions>, PhaseOneProcessingOptionsValidator>();

        services.AddScoped<IJobWorkspaceManager, JobWorkspaceManager>();
        services.AddScoped<IArchiveEntryFilter, ArchiveEntryFilter>();
        services.AddScoped<IProjectStackDetector, ProjectStackDetector>();
        services.AddScoped<IArchiveExtractor, SecureArchiveExtractor>();
        services.AddScoped<IFileTreeBuilder, FileTreeBuilder>();
        services.AddScoped<IProjectRootDiscoverer, ProjectRootDiscoverer>();
        services.AddScoped<IPhaseOneProcessor, PhaseOneProcessor>();

        services.AddScoped<IManifestParser, ManifestParser>();
        services.AddScoped<IArchitectureInferenceEngine, ArchitectureInferenceEngine>();
        services.AddScoped<ICSharpAstParser, CSharpAstParser>();
        services.AddScoped<IPhaseTwoProcessor, PhaseTwoProcessor>();
        services.AddScoped<IPipelineOrchestrator, PipelineOrchestrator>();

        services.AddOptions<PhaseThreeProcessingOptions>()
            .Bind(configuration.GetSection(PhaseThreeProcessingOptions.SectionName));

        services.AddSingleton<IValidateOptions<PhaseThreeProcessingOptions>, PhaseThreeProcessingOptionsValidator>();

        services.AddScoped<IFrontendProjectDetector, FrontendProjectDetector>();
        services.AddScoped<IUiRouteDiscoverer, UiRouteDiscoverer>();
        services.AddScoped<IUiSourceParser, UiSourceParser>();
        services.AddScoped<IUiComponentResolver, UiComponentResolver>();
        services.AddScoped<IUiScenarioExtractor, UiScenarioExtractor>();
        services.AddScoped<IPhaseThreeProcessor, PhaseThreeProcessor>();
        services.AddScoped<IFullPipelineOrchestrator, FullPipelineOrchestrator>();

        services.AddScoped<ICorrelationEngine, CorrelationEngine>();
        services.AddScoped<IPhaseFourProcessor, PhaseFourProcessor>();
        services.AddScoped<ICorrelationPipelineOrchestrator, CorrelationPipelineOrchestrator>();

        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName));

        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();

        services.AddScoped<IPromptBuilder, PromptBuilder>();
        services.AddScoped<IPhaseFiveProcessor, PhaseFiveProcessor>();
        services.AddScoped<ISynthesisPipelineOrchestrator, SynthesisPipelineOrchestrator>();

        // LLM provider selection is resolved lazily from options so that
        // integration tests can override LlmOptions.Provider via configuration.
        services.AddSingleton<FakeLlmClient>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            if (options.Provider.Equals("Fake", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<FakeLlmClient>();
            }

            return new OllamaLlmClient(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IOptions<LlmOptions>>());
        });

        // ---- Phase 6: artifact compilation, storage, and job status ----

        services.AddOptions<ArtifactStorageOptions>()
            .Bind(configuration.GetSection(ArtifactStorageOptions.SectionName));

        services.AddSingleton<IMarkdownParser, MarkdownParser>();
        services.AddSingleton<IDocumentRenderer, PdfDocumentRenderer>();
        services.AddSingleton<IDocumentRenderer, DocxDocumentRenderer>();
        services.AddSingleton<IArtifactStore, LocalFileArtifactStore>();
        services.AddSingleton<IJobStatusStore, InMemoryJobStatusStore>();
        services.AddSingleton<IJobQueue, JobQueue>();
        services.AddSingleton<IArchitectureDiagramBuilder, ArchitectureDiagramBuilder>();

        services.AddScoped<IPhaseSixProcessor, PhaseSixProcessor>();
        services.AddScoped<IDocumentPipelineService, DocumentPipelineService>();

        services.AddHostedService<JobProcessingBackgroundService>();

        return services;
    }
}
