using System.Collections.Concurrent;
using CongNoGolden.Application.Customers;
using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Services.Common;

namespace CongNoGolden.Infrastructure.Services;

public sealed class MaintenanceJobQueue : IMaintenanceJobQueue
{
    private sealed class JobState
    {
        public required Guid JobId { get; init; }
        public required MaintenanceJobType JobType { get; init; }
        public required string RequestedBy { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required CustomerBalanceReconcileRequest? ReconcileRequest { get; init; }
        public MaintenanceJobStatus Status { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public string? Summary { get; set; }
        public string? Error { get; set; }
    }

    private readonly object _sync = new();
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly ConcurrentDictionary<Guid, JobState> _jobs = new();
    private int _queuedCount;

    public MaintenanceJobSnapshot Enqueue(EnqueueMaintenanceJobRequest request)
    {
        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var state = new JobState
        {
            JobId = jobId,
            JobType = request.JobType,
            RequestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "system" : request.RequestedBy.Trim(),
            CreatedAtUtc = now,
            ReconcileRequest = request.ReconcileRequest,
            Status = MaintenanceJobStatus.Queued
        };

        lock (_sync)
        {
            _jobs[jobId] = state;
            _queue.Enqueue(jobId);
            _queuedCount++;
        }
        BusinessMetrics.SetMaintenanceQueueDepth(_queuedCount);
        BusinessMetrics.RecordMaintenanceJobEnqueued(request.JobType);

        return ToSnapshot(state);
    }

    public bool TryDequeue(out MaintenanceJobWorkItem? item)
    {
        item = null;
        while (_queue.TryDequeue(out var jobId))
        {
            var pending = Interlocked.Decrement(ref _queuedCount);
            if (pending < 0)
            {
                Interlocked.Exchange(ref _queuedCount, 0);
                pending = 0;
            }
            BusinessMetrics.SetMaintenanceQueueDepth(pending);

            if (!_jobs.TryGetValue(jobId, out var state))
            {
                continue;
            }

            if (state.Status != MaintenanceJobStatus.Queued)
            {
                continue;
            }

            item = new MaintenanceJobWorkItem(
                state.JobId,
                state.JobType,
                state.CreatedAtUtc,
                state.ReconcileRequest,
                state.RequestedBy);
            return true;
        }

        return false;
    }

    public void MarkRunning(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (_sync)
        {
            state.Status = MaintenanceJobStatus.Running;
            state.StartedAtUtc = DateTimeOffset.UtcNow;
            state.Error = null;
        }
    }

    public void MarkSucceeded(Guid jobId, string summary)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (_sync)
        {
            state.Status = MaintenanceJobStatus.Succeeded;
            state.CompletedAtUtc = DateTimeOffset.UtcNow;
            state.Summary = summary;
            state.Error = null;
        }
    }

    public void MarkFailed(Guid jobId, string error)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            return;
        }

        lock (_sync)
        {
            state.Status = MaintenanceJobStatus.Failed;
            state.CompletedAtUtc = DateTimeOffset.UtcNow;
            state.Error = error;
            state.Summary = null;
        }
    }

    public MaintenanceJobSnapshot? Get(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var state)
            ? ToSnapshot(state)
            : null;
    }

    public IReadOnlyList<MaintenanceJobSnapshot> List(int take)
    {
        var normalizedTake = take <= 0 ? 20 : Math.Min(take, 200);
        return _jobs.Values
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(normalizedTake)
            .Select(ToSnapshot)
            .ToList();
    }

    private static MaintenanceJobSnapshot ToSnapshot(JobState state)
    {
        return new MaintenanceJobSnapshot(
            state.JobId,
            state.JobType,
            state.Status,
            state.RequestedBy,
            state.CreatedAtUtc,
            state.StartedAtUtc,
            state.CompletedAtUtc,
            state.Summary,
            state.Error);
    }
}
