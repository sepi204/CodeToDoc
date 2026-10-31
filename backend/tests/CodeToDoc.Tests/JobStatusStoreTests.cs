using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Models;
using CodeToDoc.Service.Services;

namespace CodeToDoc.Tests;

public class JobStatusStoreTests
{
    [Fact]
    public void Upsert_And_Get_RoundTrips()
    {
        var store = new InMemoryJobStatusStore();
        var record = new JobRecord { JobId = "abc", Status = JobStatus.Queued, Phase = JobPhase.Queued };

        store.Upsert(record);

        var fetched = store.Get("abc");
        Assert.NotNull(fetched);
        Assert.Equal(JobStatus.Queued, fetched.Status);
        Assert.Equal("abc", fetched.JobId);
    }

    [Fact]
    public void Get_DoesNotMutateState()
    {
        var store = new InMemoryJobStatusStore();
        store.Upsert(new JobRecord { JobId = "abc", Status = JobStatus.Completed });

        var first = store.Get("abc")!;
        Assert.Equal(JobStatus.Completed, first.Status);

        var second = store.Get("abc")!;
        Assert.Equal(JobStatus.Completed, second.Status);
    }

    [Fact]
    public void MissingJob_ReturnsNull()
    {
        var store = new InMemoryJobStatusStore();
        Assert.Null(store.Get("nope"));
        Assert.False(store.TryGet("nope", out _));
    }

    [Fact]
    public void TerminalStatuses_AreRecorded()
    {
        var store = new InMemoryJobStatusStore();
        store.Upsert(new JobRecord { JobId = "abc", Status = JobStatus.Failed, CompletedAtUtc = DateTime.UtcNow });

        var fetched = store.Get("abc")!;
        Assert.Equal(JobStatus.Failed, fetched.Status);
        Assert.NotNull(fetched.CompletedAtUtc);
    }
}
