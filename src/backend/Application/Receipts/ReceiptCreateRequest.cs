namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptCreateRequest(
    string SellerTaxCode,
    string CustomerTaxCode,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount,
    string AllocationMode,
    DateOnly? AppliedPeriodStart,
    string? Method,
    string? Description,
    string? AllocationPriority,
    IReadOnlyList<ReceiptTargetRef>? SelectedTargets
);
