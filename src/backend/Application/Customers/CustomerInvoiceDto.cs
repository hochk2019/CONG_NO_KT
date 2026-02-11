namespace CongNoGolden.Application.Customers;

public sealed record CustomerInvoiceDto(
    Guid Id,
    string InvoiceNo,
    DateOnly IssueDate,
    decimal TotalAmount,
    decimal OutstandingAmount,
    string Status,
    int Version,
    string SellerTaxCode,
    string? SellerShortName,
    IReadOnlyList<CustomerReceiptRefDto> ReceiptRefs);

public sealed record CustomerReceiptRefDto(
    Guid Id,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount);
