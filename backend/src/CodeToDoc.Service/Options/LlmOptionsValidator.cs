using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Options;

public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Provider)) failures.Add("Provider must be configured.");
        if (string.IsNullOrWhiteSpace(options.Model)) failures.Add("Model must be configured.");
        if (options.TimeoutSeconds <= 0) failures.Add("TimeoutSeconds must be greater than zero.");
        if (options.ContextWindow <= 0) failures.Add("ContextWindow must be greater than zero.");
        if (options.Temperature < 0 || options.Temperature > 1) failures.Add("Temperature must be between 0 and 1.");
        if (options.MaxInputTokens <= 0) failures.Add("MaxInputTokens must be greater than zero.");
        if (options.MaxOutputTokens <= 0) failures.Add("MaxOutputTokens must be greater than zero.");
        if (options.MaxRetries < 0) failures.Add("MaxRetries must be non-negative.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
