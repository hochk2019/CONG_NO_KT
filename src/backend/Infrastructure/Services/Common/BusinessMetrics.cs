using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using CongNoGolden.Application.Maintenance;

namespace CongNoGolden.Infrastructure.Services.Common;

public static class BusinessMetrics
{
    public const string MeterName = "CongNoGolden.Business";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> ReceiptApprovalCounter = Meter.CreateCounter<long>(
        "congno_receipt_approval_total",
        unit: "operation",
        description: "Number of receipt approval operations grouped by outcome.");

    private static readonly Histogram<double> ReceiptAllocationCoverageHistogram = Meter.CreateHistogram<double>(
        "congno_receipt_allocation_coverage_ratio",
        unit: "ratio",
        description: "Coverage ratio of receipt allocation (1 = fully allocated).");

    private static readonly Counter<long> ReminderRunCounter = Meter.CreateCounter<long>(
        "congno_reminder_run_total",
        unit: "operation",
        description: "Number of reminder runs.");

    private static readonly Counter<long> ReminderOutcomeCounter = Meter.CreateCounter<long>(
        "congno_reminder_outcome_total",
        unit: "operation",
        description: "Reminder outcomes aggregated by status and dry-run mode.");

    private static readonly Counter<long> ImportCommitCounter = Meter.CreateCounter<long>(
        "congno_import_commit_total",
        unit: "operation",
        description: "Number of import commit operations.");

    private static readonly Counter<long> ImportCommitRowsCounter = Meter.CreateCounter<long>(
        "congno_import_commit_rows_total",
        unit: "row",
        description: "Import commit rows grouped by row outcome.");

    private static readonly Counter<long> MaintenanceJobEnqueuedCounter = Meter.CreateCounter<long>(
        "congno_maintenance_job_enqueued_total",
        unit: "job",
        description: "Number of maintenance jobs enqueued by type.");

    private static readonly Counter<long> MaintenanceJobCounter = Meter.CreateCounter<long>(
        "congno_maintenance_job_total",
        unit: "job",
        description: "Number of maintenance jobs processed grouped by outcome.");

    private static readonly Histogram<double> MaintenanceQueueDelayHistogram = Meter.CreateHistogram<double>(
        "congno_maintenance_queue_delay_ms",
        unit: "ms",
        description: "Queue delay in milliseconds for maintenance jobs before execution starts.");

    private static readonly Histogram<double> MaintenanceJobDurationHistogram = Meter.CreateHistogram<double>(
        "congno_maintenance_job_duration_ms",
        unit: "ms",
        description: "Execution duration in milliseconds for maintenance jobs.");

    private static long _maintenanceQueueDepth;
    private static readonly ObservableGauge<long> MaintenanceQueueDepthGauge = Meter.CreateObservableGauge<long>(
        "congno_maintenance_queue_depth",
        observeValue: () => Interlocked.Read(ref _maintenanceQueueDepth),
        unit: "job",
        description: "Current number of queued maintenance jobs.");

    public static void RecordReceiptApprovalSuccess(string? allocationStatus, decimal amount, decimal unallocatedAmount)
    {
        var normalizedAllocationStatus = NormalizeTagValue(allocationStatus, "unknown");
        var tags = new TagList
        {
            { "outcome", "success" },
            { "allocation_status", normalizedAllocationStatus }
        };

        ReceiptApprovalCounter.Add(1, tags);

        if (amount <= 0)
        {
            return;
        }

        var boundedUnallocated = decimal.Clamp(unallocatedAmount, 0m, amount);
        var coverage = 1d - (double)(boundedUnallocated / amount);
        ReceiptAllocationCoverageHistogram.Record(coverage, tags);
    }

    public static void RecordReceiptApprovalFailure(string reason)
    {
        ReceiptApprovalCounter.Add(
            1,
            new TagList
            {
                { "outcome", "failed" },
                { "reason", NormalizeTagValue(reason, "unknown") }
            });
    }

    public static void RecordReminderRun(
        bool dryRun,
        int totalCandidates,
        int sentCount,
        int failedCount,
        int skippedCount)
    {
        var dryRunTag = dryRun ? "true" : "false";
        ReminderRunCounter.Add(
            1,
            new TagList
            {
                { "dry_run", dryRunTag },
                { "has_candidates", totalCandidates > 0 ? "true" : "false" }
            });

        if (sentCount > 0)
        {
            ReminderOutcomeCounter.Add(
                sentCount,
                new TagList
                {
                    { "dry_run", dryRunTag },
                    { "status", "sent" }
                });
        }

        if (failedCount > 0)
        {
            ReminderOutcomeCounter.Add(
                failedCount,
                new TagList
                {
                    { "dry_run", dryRunTag },
                    { "status", "failed" }
                });
        }

        if (skippedCount > 0)
        {
            ReminderOutcomeCounter.Add(
                skippedCount,
                new TagList
                {
                    { "dry_run", dryRunTag },
                    { "status", "skipped" }
                });
        }
    }

    public static void RecordImportCommit(
        string? batchType,
        int totalEligibleRows,
        int committedRows,
        int skippedRows)
    {
        var normalizedType = NormalizeTagValue(batchType, "unknown");
        ImportCommitCounter.Add(
            1,
            new TagList
            {
                { "batch_type", normalizedType },
                { "has_eligible_rows", totalEligibleRows > 0 ? "true" : "false" }
            });

        if (committedRows > 0)
        {
            ImportCommitRowsCounter.Add(
                committedRows,
                new TagList
                {
                    { "batch_type", normalizedType },
                    { "row_outcome", "committed" }
                });
        }

        if (skippedRows > 0)
        {
            ImportCommitRowsCounter.Add(
                skippedRows,
                new TagList
                {
                    { "batch_type", normalizedType },
                    { "row_outcome", "skipped" }
                });
        }
    }

    public static void RecordMaintenanceJobEnqueued(MaintenanceJobType jobType)
    {
        MaintenanceJobEnqueuedCounter.Add(
            1,
            new TagList
            {
                { "job_type", NormalizeJobType(jobType) }
            });
    }

    public static void SetMaintenanceQueueDepth(int queuedCount)
    {
        var normalized = Math.Max(0, queuedCount);
        Interlocked.Exchange(ref _maintenanceQueueDepth, normalized);
    }

    public static void RecordMaintenanceJobStarted(MaintenanceJobType jobType, TimeSpan queueDelay)
    {
        MaintenanceQueueDelayHistogram.Record(
            Math.Max(0d, queueDelay.TotalMilliseconds),
            new TagList
            {
                { "job_type", NormalizeJobType(jobType) }
            });
    }

    public static void RecordMaintenanceJobCompleted(
        MaintenanceJobType jobType,
        bool succeeded,
        TimeSpan duration,
        string? failureType = null)
    {
        var tags = new TagList
        {
            { "job_type", NormalizeJobType(jobType) },
            { "outcome", succeeded ? "success" : "failed" }
        };
 
        if (!succeeded && !string.IsNullOrWhiteSpace(failureType))
        {
            tags.Add("failure_type", NormalizeTagValue(failureType, "unknown"));
        }

        MaintenanceJobCounter.Add(1, tags);
        MaintenanceJobDurationHistogram.Record(
            Math.Max(0d, duration.TotalMilliseconds),
            tags);
    }

    private static string NormalizeTagValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeJobType(MaintenanceJobType jobType)
    {
        return NormalizeTagValue(jobType.ToString(), "unknown");
    }
}
