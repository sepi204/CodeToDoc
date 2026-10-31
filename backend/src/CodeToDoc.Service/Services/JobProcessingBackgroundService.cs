using CodeToDoc.Service.Enums;
using CodeToDoc.Service.Interfaces;
using CodeToDoc.Service.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeToDoc.Service.Services;

/// <summary>
/// Background worker that drains the job queue and runs the full Phase 1-6
/// pipeline for each job, transitioning terminal status on completion,
/// cancellation, or failure.
/// </summary>
public sealed class JobProcessingBackgroundService : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobStatusStore _statusStore;
    private readonly ILogger<JobProcessingBackgroundService> _logger;

    public JobProcessingBackgroundService(
        IJobQueue queue,
        IServiceScopeFactory scopeFactory,
        IJobStatusStore statusStore,
        ILogger<JobProcessingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _statusStore = statusStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _queue.RegisterCancellation(job.JobId, cts);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<IDocumentPipelineService>();
                await pipeline.RunAsync(job, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                MarkCancelled(job.JobId);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down; leave the job as-is.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed.", job.JobId);
                MarkFailed(job.JobId, ex.Message);
            }
            finally
            {
                _queue.Unregister(job.JobId);
            }
        }
    }

    private void MarkCancelled(string jobId)
    {
        var record = _statusStore.Get(jobId);
        if (record is null)
        {
            return;
        }

        record.Status = JobStatus.Cancelled;
        record.Phase = JobPhase.Completed;
        record.ProgressMessage = "Cancelled";
        record.CompletedAtUtc = DateTime.UtcNow;
        _statusStore.Upsert(record);
    }

    private void MarkFailed(string jobId, string message)
    {
        var record = _statusStore.Get(jobId);
        if (record is null)
        {
            return;
        }

        record.Status = JobStatus.Failed;
        record.Phase = JobPhase.Completed;
        record.ProgressMessage = "Failed";
        record.CompletedAtUtc = DateTime.UtcNow;
        if (record.Diagnostics.Count < 20)
        {
            record.Diagnostics.Add(Truncate(message, 300));
        }

        _statusStore.Upsert(record);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
