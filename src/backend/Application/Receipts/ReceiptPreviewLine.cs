namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptPreviewLine(
    Guid TargetId,
    string TargetType,
    decimal Amount
);
