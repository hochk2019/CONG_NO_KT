namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditListItem(
    Guid Id,
    int Version,
    string Status,
    Guid ReceiptId,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    Guid OriginalInvoiceId,
    string? OriginalInvoiceNo,
    DateOnly? OriginalInvoiceDate,
    decimal OriginalAmount,
    decimal AmountRemaining,
    decimal AppliedAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
