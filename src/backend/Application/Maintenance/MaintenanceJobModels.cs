using CongNoGolden.Application.Customers;

namespace CongNoGolden.Application.Maintenance;

public enum MaintenanceJobType
{
    ReconcileBalances = 1,
    RunRetention = 2
}

public enum MaintenanceJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4
}

public sealed record MaintenanceJobWorkItem(
    Guid JobId,
    MaintenanceJobType JobType,
    DateTimeOffset CreatedAtUtc,
    CustomerBalanceReconcileRequest? ReconcileRequest,
    string RequestedBy);

public sealed record MaintenanceJobSnapshot(
    Guid JobId,
    MaintenanceJobType JobType,
    MaintenanceJobStatus Status,
    string RequestedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Summary,
    string? Error);

public sealed record EnqueueMaintenanceJobRequest(
    MaintenanceJobType JobType,
    CustomerBalanceReconcileRequest? ReconcileRequest,
    string RequestedBy);
