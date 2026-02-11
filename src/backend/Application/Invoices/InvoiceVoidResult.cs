namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceVoidResult(
    Guid Id,
    string Status,
    int Version,
    decimal OutstandingAmount,
    Guid? ReplacementInvoiceId);
