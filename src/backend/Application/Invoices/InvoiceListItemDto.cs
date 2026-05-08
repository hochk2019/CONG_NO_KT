namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceListItemDto(
    Guid Id,
    string InvoiceNo,
    DateOnly IssueDate,
    decimal TotalAmount,
    decimal OutstandingAmount,
    string Status,
    int Version,
    string CustomerTaxCode,
    string CustomerName,
    string SellerTaxCode,
    string? SellerShortName,
    IReadOnlyList<InvoiceReceiptRefDto> ReceiptRefs,
    IReadOnlyList<InvoiceRefDto> ReductionInvoiceRefs,
    IReadOnlyList<InvoiceRefDto> ReducedInvoiceRefs);

public sealed record InvoiceReceiptRefDto(
    Guid Id,
    string ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount);

public sealed record InvoiceRefDto(
    Guid Id,
    string InvoiceNo,
    DateOnly IssueDate,
    decimal Amount);
