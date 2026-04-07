namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditReleaseResult(
    Guid HeldCreditId,
    int Version,
    string Status,
    Guid ReceiptId,
    decimal ReleasedAmount,
    decimal RemainingHeldAmount,
    decimal ReceiptUnallocatedAmount);
