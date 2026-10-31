using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Options;

public sealed class PhaseOneProcessingOptionsValidator : IValidateOptions<PhaseOneProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, PhaseOneProcessingOptions options)
    {
        var failures = new List<string>();

        if (options.MaxArchiveCount <= 0) failures.Add("MaxArchiveCount must be greater than zero.");
        if (options.MaxArchiveSizeBytes <= 0) failures.Add("MaxArchiveSizeBytes must be greater than zero.");
        if (options.MaxTotalUploadedBytes <= 0) failures.Add("MaxTotalUploadedBytes must be greater than zero.");
        if (options.MaxEntryCount <= 0) failures.Add("MaxEntryCount must be greater than zero.");
        if (options.MaxExtractedFileCount <= 0) failures.Add("MaxExtractedFileCount must be greater than zero.");
        if (options.MaxSingleExtractedFileBytes <= 0) failures.Add("MaxSingleExtractedFileBytes must be greater than zero.");
        if (options.MaxTotalExtractedBytes <= 0) failures.Add("MaxTotalExtractedBytes must be greater than zero.");
        if (options.MaxCompressionRatio <= 0) failures.Add("MaxCompressionRatio must be greater than zero.");
        if (options.MaxPathLength <= 0) failures.Add("MaxPathLength must be greater than zero.");
        if (options.AllowedArchiveExtensions is null || options.AllowedArchiveExtensions.Count == 0)
        {
            failures.Add("At least one allowed archive extension must be configured.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
