namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptPreviewResult(
    IReadOnlyList<ReceiptPreviewLine> Lines,
    decimal UnallocatedAmount
);
