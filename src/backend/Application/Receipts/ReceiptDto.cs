namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptDto(
    Guid Id,
    string Status,
    int Version,
    decimal Amount,
    decimal UnallocatedAmount,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    DateOnly? AppliedPeriodStart,
    string AllocationMode,
    string AllocationStatus,
    string AllocationPriority,
    string? AllocationSource,
    DateTimeOffset? AllocationSuggestedAt,
    IReadOnlyList<ReceiptTargetRef>? SelectedTargets,
    string Method,
    string SellerTaxCode,
    string CustomerTaxCode
);

public sealed record ReceiptAllocationDetailDto(
    string TargetType,
    Guid TargetId,
    string? TargetNo,
    DateOnly TargetDate,
    decimal Amount);
