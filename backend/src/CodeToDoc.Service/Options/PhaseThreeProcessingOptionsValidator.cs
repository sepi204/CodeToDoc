using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Options;

public sealed class PhaseThreeProcessingOptionsValidator : IValidateOptions<PhaseThreeProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, PhaseThreeProcessingOptions options)
    {
        var failures = new List<string>();

        if (options.MaxSourceFileBytes <= 0) failures.Add("MaxSourceFileBytes must be greater than zero.");
        if (options.MaxAnalyzedFiles <= 0) failures.Add("MaxAnalyzedFiles must be greater than zero.");
        if (options.MaxComponentDepth <= 0) failures.Add("MaxComponentDepth must be greater than zero.");
        if (options.MaxWorkBudget <= 0) failures.Add("MaxWorkBudget must be greater than zero.");
        if (options.MaxExtractedElements <= 0) failures.Add("MaxExtractedElements must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
