using System.Collections.Concurrent;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Services;

/// <summary>
/// In-memory job status store. Records are immutable snapshots; reading
/// (Get/TryGet) never mutates state.
/// </summary>
public sealed class InMemoryJobStatusStore : IJobStatusStore
{
    private readonly ConcurrentDictionary<string, JobRecord> _records = new();

    public JobRecord? Get(string jobId) => _records.TryGetValue(jobId, out var record) ? record : null;

    public bool TryGet(string jobId, out JobRecord? record) => _records.TryGetValue(jobId, out record);

    public void Upsert(JobRecord record)
    {
        _records[record.JobId] = record;
    }
}
