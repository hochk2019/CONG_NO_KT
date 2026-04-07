namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditApplyResult(
    Guid HeldCreditId,
    int Version,
    string Status,
    Guid InvoiceId,
    decimal AppliedHeldAmount,
    decimal AppliedGeneralCreditAmount,
    decimal RemainingHeldAmount,
    decimal InvoiceOutstandingAmount);
