using System.Threading.Channels;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>
/// In-process job queue. Enqueues jobs, exposes them to the background worker,
/// and owns per-job cancellation sources for the cancel endpoint.
/// </summary>
public interface IJobQueue
{
    ValueTask EnqueueAsync(JobRequest job, CancellationToken cancellationToken = default);

    IAsyncEnumerable<JobRequest> ReadAllAsync(CancellationToken cancellationToken);

    void RegisterCancellation(string jobId, CancellationTokenSource cts);

    bool Cancel(string jobId);

    void Unregister(string jobId);
}
