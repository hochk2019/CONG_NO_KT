namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptPreviewRequest(
    string SellerTaxCode,
    string CustomerTaxCode,
    decimal Amount,
    string AllocationMode,
    DateOnly? AppliedPeriodStart,
    IReadOnlyList<ReceiptTargetRef>? SelectedTargets
);
