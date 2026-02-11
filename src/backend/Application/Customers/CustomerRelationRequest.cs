namespace CongNoGolden.Application.Customers;

public sealed record CustomerRelationRequest(
    string? Status,
    string? Search,
    string? DocumentNo,
    string? ReceiptNo,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize);
