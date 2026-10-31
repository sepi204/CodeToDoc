using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

public interface IJobWorkspaceManager
{
    /// <summary>
    /// Creates a job workspace. When <paramref name="jobId"/> is provided it is
    /// reused (so callers can pre-assign a job id); otherwise a new id is generated.
    /// </summary>
    JobWorkspace CreateWorkspace(string? jobId = null);

    void DeleteWorkspace(string jobRootPath);
}
