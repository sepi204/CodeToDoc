using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeToDoc.Service.Services;

public sealed class JobWorkspaceManager : IJobWorkspaceManager
{
    private readonly PhaseOneProcessingOptions _options;
    private readonly ILogger<JobWorkspaceManager> _logger;

    public JobWorkspaceManager(
        IOptions<PhaseOneProcessingOptions> options,
        ILogger<JobWorkspaceManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public JobWorkspace CreateWorkspace(string? jobId = null)
    {
        var root = string.IsNullOrWhiteSpace(_options.TempRootPath)
            ? Path.Combine(Path.GetTempPath(), "codetodoc")
            : _options.TempRootPath;

        var resolvedJobId = string.IsNullOrWhiteSpace(jobId) ? Guid.NewGuid().ToString("N") : jobId;
        var jobRootPath = Path.Combine(root, resolvedJobId);
        var sourcePath = Path.Combine(jobRootPath, "source");

        Directory.CreateDirectory(sourcePath);

        return new JobWorkspace
        {
            JobId = resolvedJobId,
            JobRootPath = jobRootPath,
            SourcePath = sourcePath,
        };
    }

    public void DeleteWorkspace(string jobRootPath)
    {
        try
        {
            if (Directory.Exists(jobRootPath))
            {
                Directory.Delete(jobRootPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete job workspace directory {JobRootPath}.", jobRootPath);
        }
    }
}
