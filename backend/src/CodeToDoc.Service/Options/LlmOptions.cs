namespace CodeToDoc.Service.Options;

public class LlmOptions
{
    public const string SectionName = "LlmOptions";

    public string Provider { get; set; } = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "gemma3:4b";

    public int TimeoutSeconds { get; set; } = 120;

    public int ContextWindow { get; set; } = 8192;

    public double Temperature { get; set; } = 0.1;

    public int MaxInputTokens { get; set; } = 8000;

    public int MaxOutputTokens { get; set; } = 2000;

    public int MaxModules { get; set; } = 50;

    public int MaxCalls { get; set; } = 200;

    public int MaxRetries { get; set; } = 2;

    public string TechnicalDocumentLanguage { get; set; } = "en";

    public string UserGuideLanguage { get; set; } = "fa";
}
