using CodeToDoc.Service.Models;

namespace CodeToDoc.Service.Interfaces;

/// <summary>Reads and writes job lifecycle records. Reads never mutate state.</summary>
public interface IJobStatusStore
{
    JobRecord? Get(string jobId);

    bool TryGet(string jobId, out JobRecord? record);

    void Upsert(JobRecord record);
}
