using CongNoGolden.Infrastructure.Services.Common;
using CongNoGolden.Application.Maintenance;
using Xunit;

namespace Tests.Unit;

public sealed class BusinessMetricsTests
{
    [Fact]
    public void RecordReceiptApprovalMethods_DoNotThrow()
    {
        BusinessMetrics.RecordReceiptApprovalSuccess("ALLOCATED", 100m, 0m);
        BusinessMetrics.RecordReceiptApprovalFailure("concurrency");
    }

    [Fact]
    public void RecordReminderRun_DoesNotThrow()
    {
        BusinessMetrics.RecordReminderRun(
            dryRun: true,
            totalCandidates: 10,
            sentCount: 8,
            failedCount: 1,
            skippedCount: 1);
    }

    [Fact]
    public void RecordImportCommit_DoesNotThrow()
    {
        BusinessMetrics.RecordImportCommit("INVOICE", totalEligibleRows: 20, committedRows: 15, skippedRows: 5);
    }

    [Fact]
    public void RecordMaintenanceMetrics_DoesNotThrow()
    {
        BusinessMetrics.RecordMaintenanceJobEnqueued(MaintenanceJobType.ReconcileBalances);
        BusinessMetrics.SetMaintenanceQueueDepth(3);
        BusinessMetrics.RecordMaintenanceJobStarted(MaintenanceJobType.ReconcileBalances, TimeSpan.FromSeconds(5));
        BusinessMetrics.RecordMaintenanceJobCompleted(
            MaintenanceJobType.ReconcileBalances,
            succeeded: true,
            duration: TimeSpan.FromSeconds(2));
        BusinessMetrics.RecordMaintenanceJobCompleted(
            MaintenanceJobType.RunRetention,
            succeeded: false,
            duration: TimeSpan.FromSeconds(1),
            failureType: "timeout");
    }
}
