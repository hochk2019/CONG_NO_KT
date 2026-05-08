namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceListRequest(
    string? Status,
    string? Search,
    string? DocumentNo,
    string? ReceiptNo,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize);
