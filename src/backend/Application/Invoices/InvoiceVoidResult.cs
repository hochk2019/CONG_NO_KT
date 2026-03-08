namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceVoidResult(
    Guid Id,
    string Status,
    int Version,
    decimal OutstandingAmount,
    Guid? ReplacementInvoiceId,
    decimal HeldCreditAmount = 0,
    int HeldCreditCount = 0,
    decimal RestoredHeldCreditAmount = 0,
    int RestoredHeldCreditCount = 0);
