using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>In-process job queue with per-job cancellation tracking.</summary>
public sealed class JobQueue : IJobQueue
{
    private readonly Channel<JobRequest> _channel =
        Channel.CreateBounded<JobRequest>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public ValueTask EnqueueAsync(JobRequest job, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public async IAsyncEnumerable<JobRequest> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return job;
        }
    }

    public void RegisterCancellation(string jobId, CancellationTokenSource cts) =>
        _cancellations[jobId] = cts;

    public bool Cancel(string jobId)
    {
        if (_cancellations.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        return false;
    }

    public void Unregister(string jobId) => _cancellations.TryRemove(jobId, out _);
}
