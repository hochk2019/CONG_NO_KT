using CongNoGolden.Application.Customers;
using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace Tests.Unit;

public sealed class MaintenanceJobQueueTests
{
    [Fact]
    public void Enqueue_And_Process_Succeeds()
    {
        var queue = new MaintenanceJobQueue();
        var snapshot = queue.Enqueue(new EnqueueMaintenanceJobRequest(
            JobType: MaintenanceJobType.ReconcileBalances,
            ReconcileRequest: new CustomerBalanceReconcileRequest(
                ApplyChanges: true,
                MaxItems: 50,
                Tolerance: 0.02m),
            RequestedBy: "tester"));

        Assert.Equal(MaintenanceJobStatus.Queued, snapshot.Status);
        Assert.Equal("tester", snapshot.RequestedBy);

        var listed = queue.List(10);
        Assert.Single(listed);
        Assert.Equal(snapshot.JobId, listed[0].JobId);

        var hasItem = queue.TryDequeue(out var item);
        Assert.True(hasItem);
        Assert.NotNull(item);
        Assert.Equal(snapshot.JobId, item!.JobId);
        Assert.Equal(snapshot.JobType, item.JobType);
        Assert.Equal(snapshot.CreatedAtUtc, item.CreatedAtUtc);
        Assert.Equal("tester", item.RequestedBy);

        queue.MarkRunning(item.JobId);
        var running = queue.Get(item.JobId);
        Assert.NotNull(running);
        Assert.Equal(MaintenanceJobStatus.Running, running!.Status);
        Assert.NotNull(running.StartedAtUtc);

        queue.MarkSucceeded(item.JobId, "ok");
        var completed = queue.Get(item.JobId);
        Assert.NotNull(completed);
        Assert.Equal(MaintenanceJobStatus.Succeeded, completed!.Status);
        Assert.Equal("ok", completed.Summary);
        Assert.Null(completed.Error);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [Fact]
    public void MarkFailed_SetsError_And_ClearsSummary()
    {
        var queue = new MaintenanceJobQueue();
        var snapshot = queue.Enqueue(new EnqueueMaintenanceJobRequest(
            JobType: MaintenanceJobType.RunRetention,
            ReconcileRequest: null,
            RequestedBy: "ops"));

        Assert.True(queue.TryDequeue(out var item));
        Assert.NotNull(item);

        queue.MarkRunning(snapshot.JobId);
        queue.MarkFailed(snapshot.JobId, "boom");

        var failed = queue.Get(snapshot.JobId);
        Assert.NotNull(failed);
        Assert.Equal(MaintenanceJobStatus.Failed, failed!.Status);
        Assert.Equal("boom", failed.Error);
        Assert.Null(failed.Summary);
        Assert.NotNull(failed.CompletedAtUtc);
    }
}
