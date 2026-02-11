namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptVoidResult(
    decimal ReversedAmount,
    int ReversedAllocations
);
