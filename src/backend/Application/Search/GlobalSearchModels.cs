namespace CongNoGolden.Application.Search;

public sealed record GlobalSearchResultDto(
    string Query,
    int Total,
    IReadOnlyList<GlobalSearchCustomerItem> Customers,
    IReadOnlyList<GlobalSearchInvoiceItem> Invoices,
    IReadOnlyList<GlobalSearchReceiptItem> Receipts);

public sealed record GlobalSearchCustomerItem(
    string TaxCode,
    string Name);

public sealed record GlobalSearchInvoiceItem(
    Guid Id,
    string InvoiceNo,
    string CustomerTaxCode,
    string CustomerName,
    DateOnly IssueDate,
    decimal OutstandingAmount,
    string Status);

public sealed record GlobalSearchReceiptItem(
    Guid Id,
    string? ReceiptNo,
    string CustomerTaxCode,
    string CustomerName,
    DateOnly ReceiptDate,
    decimal Amount,
    string Status);
