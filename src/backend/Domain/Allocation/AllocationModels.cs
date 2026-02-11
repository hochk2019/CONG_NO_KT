namespace CongNoGolden.Domain.Allocation;

public enum AllocationMode
{
    ByInvoice,
    ByPeriod,
    Fifo,
    Manual
}

public enum AllocationTargetType
{
    Invoice,
    Advance
}

public sealed record AllocationTarget(
    Guid Id,
    AllocationTargetType TargetType,
    DateOnly IssueDate,
    decimal OutstandingAmount
);

public sealed record AllocationRequest(
    decimal Amount,
    AllocationMode Mode,
    DateOnly? AppliedPeriodStart,
    IReadOnlyList<AllocationTargetRef>? SelectedTargets
);

public sealed record AllocationTargetRef(
    Guid Id,
    AllocationTargetType TargetType
);

public sealed record AllocationLine(
    Guid TargetId,
    AllocationTargetType TargetType,
    decimal Amount
);

public sealed record AllocationResult(
    IReadOnlyList<AllocationLine> Lines,
    decimal UnallocatedAmount
);
