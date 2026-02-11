namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptTargetRef(
    Guid Id,
    string TargetType
);
